using System.Data;
using System.Data.Common;
using System.Text.Json;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts;
using Construction.Platform.Core.Application.Approvals;
using Construction.Platform.Core.Domain.Approvals;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace Construction.Platform.Infrastructure.Approvals;

/// <summary>
/// PLT-208 one-step approval slice: deterministic amount routing, immutable snapshot, server-side
/// segregation of duties, row-version concurrency and append-only decisions. The owning module's
/// business aggregate is never touched; the outcome leaves through the outbox.
/// </summary>
public sealed class PostgresApprovalService : IApprovalService
{
    private const int MaxCommentLength = 2000;

    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;
    private readonly IApproverDirectory _directory;
    private readonly TimeProvider _clock;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PostgresApprovalService(
        IConfiguration configuration,
        ITenantContext tenantContext,
        IApproverDirectory directory,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Result<ApprovalInstanceDto>> RequestAsync(
        CreateApprovalRequestRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = ValidateRequest(request, actorId);
        if (validation is not null)
        {
            return Result.Failure<ApprovalInstanceDto>(validation);
        }

        var tenantId = CurrentTenant();
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var policies = await LoadPoliciesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var match = ApprovalPolicyMatcher.Match(
            policies,
            new ApprovalMatchInput(
                request.RecordType,
                request.Amount,
                request.Currency,
                request.ProjectId,
                request.Trade,
                DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime)));

        if (match.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalInstanceDto>(match.Error);
        }

        var policy = match.Value;
        var step = policy.Steps.OrderBy(s => s.Order).First();
        var approvers = await ResolveApproversAsync(
            tenantId, request, step, cancellationToken).ConfigureAwait(false);

        approvers = approvers.Where(id => id != actorId).Distinct().ToArray();
        if (approvers.Length == 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalInstanceDto>(Error.Forbidden(
                "platform.approval_self_approval_denied",
                "No eligible approver remains after segregation of duties."));
        }

        var instanceId = Guid.CreateVersion7();
        var stepId = Guid.CreateVersion7();

        try
        {
            await using (var insert = Command(connection, transaction, """
                INSERT INTO platform.approval_instances
                    (id, tenant_id, record_type, record_id, project_id, requester_id, status,
                     policy_id, policy_code, policy_version, amount, currency, context)
                VALUES
                    (@id, @tenantId, @recordType, @recordId, @projectId, @requesterId, 'pending',
                     @policyId, @policyCode, @policyVersion, @amount, @currency, @context::jsonb);
                """))
            {
                Add(insert, "id", instanceId);
                Add(insert, "tenantId", tenantId);
                Add(insert, "recordType", request.RecordType);
                Add(insert, "recordId", request.RecordId);
                Add(insert, "projectId", (object?)request.ProjectId ?? DBNull.Value);
                Add(insert, "requesterId", actorId);
                Add(insert, "policyId", policy.Id);
                Add(insert, "policyCode", policy.Code);
                Add(insert, "policyVersion", policy.Version);
                Add(insert, "amount", request.Amount);
                Add(insert, "currency", request.Currency.ToUpperInvariant());
                Add(insert, "context", (object?)request.Context ?? DBNull.Value);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var insertStep = Command(connection, transaction, """
                INSERT INTO platform.approval_steps
                    (id, tenant_id, instance_id, step_order, status, approver_ids)
                VALUES (@id, @tenantId, @instanceId, 1, 'pending', @approvers);
                """))
            {
                Add(insertStep, "id", stepId);
                Add(insertStep, "tenantId", tenantId);
                Add(insertStep, "instanceId", instanceId);
                AddApprovers(insertStep, approvers);
                await insertStep.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await PublishAsync(
                connection, transaction, "platform.approval.requested.v1", instanceId, tenantId,
                request, actorId, "pending", cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalInstanceDto>(Error.Conflict(
                "platform.approval_invalid_state",
                "An open approval instance already exists for this record."));
        }

        var created = await LoadInstanceAsync(connection, null, instanceId, cancellationToken).ConfigureAwait(false);
        return Result.Success(created!);
    }

    public async Task<ApprovalInstanceDto?> GetAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var dto = await LoadInstanceAsync(connection, transaction, instanceId, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return dto;
    }

    public async Task<Result<ApprovalInstanceDto>> DecideAsync(
        Guid instanceId,
        bool approve,
        string? comment,
        int expectedRowVersion,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
        {
            return Result.Failure<ApprovalInstanceDto>(Error.Unauthorized(
                "platform.approval_not_approver", "An authenticated user is required."));
        }

        if (!approve && string.IsNullOrWhiteSpace(comment))
        {
            return Result.Failure<ApprovalInstanceDto>(Error.Validation(
                "platform.approval_reason_required", "A rejection reason is required."));
        }

        if (comment is { Length: > MaxCommentLength })
        {
            return Result.Failure<ApprovalInstanceDto>(Error.Validation(
                "platform.approval_reason_required", "The comment is too long."));
        }

        var tenantId = CurrentTenant();
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var instance = await LoadInstanceAsync(connection, transaction, instanceId, cancellationToken)
            .ConfigureAwait(false);
        if (instance is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalInstanceDto>(Error.NotFound(
                "platform.approval_not_found", "The approval instance was not found."));
        }

        if (!string.Equals(instance.Status, "pending", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalInstanceDto>(Error.Conflict(
                "platform.approval_already_decided", "The approval has already been decided."));
        }

        if (instance.RowVersion != expectedRowVersion)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalInstanceDto>(Error.PreconditionFailed(
                "platform.precondition_failed", "The approval was modified by another request."));
        }

        if (!instance.ApproverIds.Contains(actorId))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalInstanceDto>(Error.Forbidden(
                "platform.approval_not_approver", "The current user is not an approver for this step."));
        }

        var sod = ApprovalPolicyMatcher.ValidateSelfApproval(
            instance.RecordType, instance.RequestedBy, actorId, tenantAllowsSelfApproval: false);
        if (sod is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalInstanceDto>(sod);
        }

        var stepId = await LockStepAsync(connection, transaction, instanceId, cancellationToken)
            .ConfigureAwait(false);
        if (stepId is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalInstanceDto>(Error.Conflict(
                "platform.approval_already_decided", "The approval step has already been decided."));
        }

        var status = approve ? "approved" : "rejected";
        var action = approve ? "approve" : "reject";

        await using (var decision = Command(connection, transaction, """
            INSERT INTO platform.approval_decisions
                (id, tenant_id, instance_id, step_id, actor_id, action, comment)
            VALUES (@id, @tenantId, @instanceId, @stepId, @actorId, @action, @comment);
            """))
        {
            Add(decision, "id", Guid.CreateVersion7());
            Add(decision, "tenantId", tenantId);
            Add(decision, "instanceId", instanceId);
            Add(decision, "stepId", stepId.Value);
            Add(decision, "actorId", actorId);
            Add(decision, "action", action);
            Add(decision, "comment", (object?)comment ?? DBNull.Value);
            await decision.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var updateStep = Command(connection, transaction, """
            UPDATE platform.approval_steps
            SET status = @status, decided_by = @actorId, decided_at = now(), comment = @comment
            WHERE id = @stepId;
            """))
        {
            Add(updateStep, "status", status);
            Add(updateStep, "actorId", actorId);
            Add(updateStep, "comment", (object?)comment ?? DBNull.Value);
            Add(updateStep, "stepId", stepId.Value);
            await updateStep.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var updateInstance = Command(connection, transaction, """
            UPDATE platform.approval_instances
            SET status = @status, decided_at = now(), decided_by = @actorId, row_version = row_version + 1
            WHERE id = @id;
            """))
        {
            Add(updateInstance, "status", status);
            Add(updateInstance, "actorId", actorId);
            Add(updateInstance, "id", instanceId);
            await updateInstance.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await PublishAsync(
            connection,
            transaction,
            approve ? "platform.approval.approved.v1" : "platform.approval.rejected.v1",
            instanceId,
            tenantId,
            new CreateApprovalRequestRequest(
                instance.RecordType, instance.RecordId, instance.ProjectId,
                instance.Amount, instance.Currency, null, null, null),
            actorId,
            status,
            cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var updated = await LoadInstanceAsync(connection, null, instanceId, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(updated!);
    }

    public async Task<ApprovalSimulationResponse> SimulateAsync(
        ApprovalSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var policies = await LoadPoliciesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var match = ApprovalPolicyMatcher.Match(
            policies,
            new ApprovalMatchInput(
                request.RecordType,
                request.Amount,
                request.Currency,
                request.ProjectId,
                request.Trade,
                request.OnDate ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime)));

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (match.IsFailure)
        {
            return new ApprovalSimulationResponse(
                null, null, request.Amount, [], match.Error.Message);
        }

        var step = match.Value.Steps.OrderBy(s => s.Order).First();
        return new ApprovalSimulationResponse(
            match.Value.Code,
            match.Value.Version,
            request.Amount,
            step.ApproverIds,
            null);
    }

    public async Task<IReadOnlyList<ApprovalPolicyDto>> ListPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var policies = await LoadPoliciesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return policies
            .OrderBy(p => p.Code, StringComparer.Ordinal)
            .Select(ToPolicyDto)
            .ToArray();
    }

    public async Task<Result<ApprovalPolicyDto>> CreatePolicyAsync(
        CreateApprovalPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.RecordType) ||
            request.Currency.Length != 3 || request.MinAmount < 0 ||
            (request.MaxAmount is { } max && max <= request.MinAmount))
        {
            return Result.Failure<ApprovalPolicyDto>(Error.Validation(
                "platform.approval_invalid_state", "The approval policy is invalid."));
        }

        if (request.Steps.Count != 1)
        {
            return Result.Failure<ApprovalPolicyDto>(Error.Validation(
                "platform.approval_invalid_state",
                "This slice supports exactly one approval step per policy."));
        }

        var tenantId = CurrentTenant();
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var existing = await LoadPoliciesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var policyId = Guid.CreateVersion7();

        var candidate = new ApprovalPolicyCandidate(
            policyId,
            request.Code,
            request.RecordType,
            request.MinAmount,
            request.MaxAmount,
            request.Currency.ToUpperInvariant(),
            request.ProjectId,
            request.Trade,
            request.Priority,
            request.IsActive,
            request.EffectiveFrom,
            request.EffectiveTo,
            1,
            request.Steps.Select((s, index) => new ApprovalStepSpec(
                index + 1, s.ApproverSource, s.ApproverIds, s.LimitAmount)).ToArray());

        var overlap = ApprovalPolicyMatcher.ValidateNoOverlap(existing, candidate);
        if (overlap is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<ApprovalPolicyDto>(overlap);
        }

        await using (var insert = Command(connection, transaction, """
            INSERT INTO platform.approval_policies
                (id, tenant_id, code, record_type, min_amount, max_amount, currency,
                 project_id, trade, priority, is_active, effective_from, effective_to, version)
            VALUES
                (@id, @tenantId, @code, @recordType, @min, @max, @currency,
                 @projectId, @trade, @priority, @isActive, @from, @to, 1);
            """))
        {
            Add(insert, "id", policyId);
            Add(insert, "tenantId", tenantId);
            Add(insert, "code", request.Code);
            Add(insert, "recordType", request.RecordType);
            Add(insert, "min", request.MinAmount);
            Add(insert, "max", (object?)request.MaxAmount ?? DBNull.Value);
            Add(insert, "currency", request.Currency.ToUpperInvariant());
            Add(insert, "projectId", (object?)request.ProjectId ?? DBNull.Value);
            Add(insert, "trade", (object?)request.Trade ?? DBNull.Value);
            Add(insert, "priority", request.Priority);
            Add(insert, "isActive", request.IsActive);
            Add(insert, "from", request.EffectiveFrom);
            Add(insert, "to", (object?)request.EffectiveTo ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var step in candidate.Steps)
        {
            await using var insertStep = Command(connection, transaction, """
                INSERT INTO platform.approval_policy_steps
                    (id, tenant_id, policy_id, step_order, approver_source, approver_ids, limit_amount)
                VALUES (@id, @tenantId, @policyId, @order, @source, @approvers, @limit);
                """);
            Add(insertStep, "id", Guid.CreateVersion7());
            Add(insertStep, "tenantId", tenantId);
            Add(insertStep, "policyId", policyId);
            Add(insertStep, "order", step.Order);
            Add(insertStep, "source", step.ApproverSource);
            AddApprovers(insertStep, step.ApproverIds.ToArray());
            Add(insertStep, "limit", (object?)step.LimitAmount ?? DBNull.Value);
            await insertStep.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToPolicyDto(candidate));
    }

    private async Task<Guid[]> ResolveApproversAsync(
        Guid tenantId,
        CreateApprovalRequestRequest request,
        ApprovalStepSpec step,
        CancellationToken cancellationToken)
    {
        var resolved = await _directory.ResolveAsync(
            new ApprovalResolutionRequest(
                tenantId,
                request.RecordType,
                request.RecordId,
                request.ProjectId,
                request.Amount,
                request.Currency,
                [step.ApproverSource]),
            cancellationToken).ConfigureAwait(false);

        var ids = resolved.Where(c => c.IsActive).Select(c => c.UserId).ToArray();
        return ids.Length > 0 ? ids : step.ApproverIds.ToArray();
    }

    private static Error? ValidateRequest(CreateApprovalRequestRequest request, Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            return Error.Unauthorized("platform.approval_not_approver", "An authenticated user is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RecordType) || request.RecordId == Guid.Empty)
        {
            return Error.Validation(
                "platform.approval_invalid_state", "recordType and recordId are required.");
        }

        if (request.Amount <= 0)
        {
            return Error.Validation(
                "platform.approval_invalid_state", "The request amount must be positive.");
        }

        if (request.Currency.Length != 3)
        {
            return Error.Validation(
                "platform.approval_invalid_state", "The currency must be a three-letter code.");
        }

        return null;
    }

    /// <summary>
    /// Writes the integration event into <c>platform.outbox</c> on the decision transaction, so the
    /// state change and its event commit together (BR-PLT-60). Consumers deserialize the payload with
    /// their registered event type.
    /// </summary>
    private async Task PublishAsync(
        DbConnection connection,
        DbTransaction transaction,
        string eventType,
        Guid instanceId,
        Guid tenantId,
        CreateApprovalRequestRequest request,
        Guid actorId,
        string status,
        CancellationToken cancellationToken)
    {
        var payload = new ApprovalEventPayload(
            instanceId,
            request.RecordType,
            request.RecordId,
            status,
            request.Amount,
            request.Currency.ToUpperInvariant());

        await using var command = Command(connection, transaction, """
            INSERT INTO platform.outbox
                (id, tenant_id, aggregate_type, aggregate_id, aggregate_version, type, payload,
                 occurred_at, attempts, next_attempt_at, correlation_id, causation_id, actor_id, project_id)
            VALUES
                (@id, @tenantId, 'approval_instance', @aggregateId, 1, @type, @payload::jsonb,
                 now(), 0, now(), @correlationId, NULL, @actorId, @projectId);
            """);
        Add(command, "id", Guid.CreateVersion7());
        Add(command, "tenantId", tenantId);
        Add(command, "aggregateId", instanceId);
        Add(command, "type", eventType);
        Add(command, "payload", JsonSerializer.Serialize(payload, _json));
        Add(command, "correlationId", Guid.CreateVersion7());
        Add(command, "actorId", actorId);
        Add(command, "projectId", (object?)request.ProjectId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ApprovalInstanceDto?> LoadInstanceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, """
            SELECT i.id, i.record_type, i.record_id, i.project_id, i.amount, i.currency,
                   i.status, i.requester_id, i.policy_code, i.policy_version, i.row_version,
                   i.requested_at, s.approver_ids, s.status
            FROM platform.approval_instances i
            LEFT JOIN platform.approval_steps s ON s.instance_id = i.id AND s.step_order = 1
            WHERE i.id = @id;
            """);
        Add(command, "id", instanceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ApprovalInstanceDto(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.GetDecimal(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetGuid(7),
            reader.GetString(8),
            reader.GetInt32(9),
            reader.GetInt32(10),
            reader.GetFieldValue<DateTimeOffset>(11),
            reader.IsDBNull(12) ? [] : reader.GetFieldValue<Guid[]>(12),
            reader.IsDBNull(13) ? null : reader.GetString(13));
    }

    private static async Task<Guid?> LockStepAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, """
            SELECT id FROM platform.approval_steps
            WHERE instance_id = @id AND step_order = 1 AND status = 'pending'
            FOR UPDATE;
            """);
        Add(command, "id", instanceId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : (Guid)value;
    }

    private static async Task<IReadOnlyList<ApprovalPolicyCandidate>> LoadPoliciesAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var steps = new Dictionary<Guid, List<ApprovalStepSpec>>();

        await using (var stepCommand = Command(connection, transaction, """
            SELECT policy_id, step_order, approver_source, approver_ids, limit_amount
            FROM platform.approval_policy_steps
            ORDER BY step_order;
            """))
        {
            await using var reader = await stepCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var policyId = reader.GetGuid(0);
                if (!steps.TryGetValue(policyId, out var list))
                {
                    list = [];
                    steps[policyId] = list;
                }

                list.Add(new ApprovalStepSpec(
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? [] : reader.GetFieldValue<Guid[]>(3),
                    reader.IsDBNull(4) ? null : reader.GetDecimal(4)));
            }
        }

        var policies = new List<ApprovalPolicyCandidate>();
        await using (var command = Command(connection, transaction, """
            SELECT id, code, record_type, min_amount, max_amount, currency, project_id, trade,
                   priority, is_active, effective_from, effective_to, version
            FROM platform.approval_policies;
            """))
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetGuid(0);
                policies.Add(new ApprovalPolicyCandidate(
                    id,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(3),
                    reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetGuid(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.GetInt32(8),
                    reader.GetBoolean(9),
                    DateOnly.FromDateTime(reader.GetFieldValue<DateTime>(10)),
                    reader.IsDBNull(11) ? null : DateOnly.FromDateTime(reader.GetFieldValue<DateTime>(11)),
                    reader.GetInt32(12),
                    steps.TryGetValue(id, out var list) ? list : []));
            }
        }

        return policies;
    }

    private static ApprovalPolicyDto ToPolicyDto(ApprovalPolicyCandidate policy) => new(
        policy.Id,
        policy.Code,
        policy.RecordType,
        policy.MinAmount,
        policy.MaxAmount,
        policy.Currency,
        policy.ProjectId,
        policy.Trade,
        policy.Priority,
        policy.IsActive,
        policy.EffectiveFrom,
        policy.EffectiveTo,
        policy.Version,
        policy.Steps
            .OrderBy(s => s.Order)
            .Select(s => new ApprovalStepDto(s.Order, s.ApproverSource, s.ApproverIds, s.LimitAmount))
            .ToArray());

    private Guid CurrentTenant()
    {
        if (!_tenantContext.HasTenant)
        {
            throw new InvalidOperationException("A tenant context is required.");
        }

        return _tenantContext.Current.Value;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT set_config('app.tenant_id', @tenantId, true);";
        command.Parameters.AddWithValue("tenantId", tenantId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DbCommand Command(
        DbConnection connection,
        DbTransaction? transaction,
        string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static void Add(DbCommand command, string name, object value) =>
        command.Parameters.Add(new NpgsqlParameter(name, value));

    private static void AddApprovers(DbCommand command, Guid[] approvers)
    {
        var parameter = new NpgsqlParameter("approvers", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = approvers
        };
        command.Parameters.Add(parameter);
    }

    private sealed record ApprovalEventPayload(
        Guid Id,
        string RecordType,
        Guid RecordId,
        string Status,
        decimal Amount,
        string Currency);
}
