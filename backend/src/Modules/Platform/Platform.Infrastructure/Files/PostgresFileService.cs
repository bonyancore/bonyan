using System.Data;
using System.Data.Common;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts;
using Construction.Platform.Core.Application.Audit;
using Construction.Platform.Core.Application.Files;
using Construction.Platform.Core.Domain.Files;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Construction.Platform.Infrastructure.Files;

/// <summary>
/// Tenant-scoped file application service. Bytes stay in object storage; the database holds only
/// metadata. A file is downloadable only when <c>available</c> (never while scanning or quarantined).
/// </summary>
public sealed class PostgresFileService : IFileService
{
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromMinutes(30);

    private static readonly string[] PostedFinancialPrefixes =
        ["cost.", "cash.", "procurement.payment", "contracts.invoice", "contracts.certificate"];

    private const int MaxFileNameLength = 400;

    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;
    private readonly IObjectStorage _storage;
    private readonly IFileScanner _scanner;
    private readonly IFileAccessChecker _access;
    private readonly IAuditLog _auditLog;
    private readonly TimeProvider _clock;

    public PostgresFileService(
        IConfiguration configuration,
        ITenantContext tenantContext,
        IObjectStorage storage,
        IFileScanner scanner,
        IFileAccessChecker access,
        IAuditLog auditLog,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Result<CreateUploadResponse>> CreateUploadAsync(
        CreateUploadRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (actorId == Guid.Empty)
        {
            return Result.Failure<CreateUploadResponse>(Error.Unauthorized("platform.file_access_denied", "An authenticated user is required."));
        }

        var type = FilePolicy.ValidateType(request.ContentType);
        if (type.IsFailure)
        {
            return Result.Failure<CreateUploadResponse>(type.Error);
        }

        var size = FilePolicy.ValidateSize(request.SizeBytes, request.Category);
        if (size.IsFailure)
        {
            return Result.Failure<CreateUploadResponse>(size.Error);
        }

        if (request.OriginalName is { Length: > MaxFileNameLength })
        {
            return Result.Failure<CreateUploadResponse>(Error.Validation("platform.file_type_not_allowed", "The file name is too long."));
        }

        var tenantId = CurrentTenant();
        var fileId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var now = _clock.GetUtcNow();
        var storageKey = FilePolicy.StorageKey(tenantId, fileId, now);
        var expiresAt = now + UploadLifetime;

        PresignedUpload presigned;
        try
        {
            presigned = await _storage.CreateUploadAsync(
                storageKey, request.ContentType, request.SizeBytes, UploadLifetime, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<CreateUploadResponse>(Error.Failure("platform.file_scan_unavailable", ex.Message));
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        // Idempotent client_ref: the same tenant + client reference returns the existing session.
        if (!string.IsNullOrWhiteSpace(request.ClientRef))
        {
            var existing = await FindSessionByClientRefAsync(
                connection, transaction, request.ClientRef, cancellationToken).ConfigureAwait(false);

            if (existing is { } found)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Result.Success(new CreateUploadResponse(
                    found.FileId,
                    found.SessionId,
                    presigned.Url,
                    found.ExpiresAt,
                    presigned.RequiredHeaders));
            }
        }

        await using (var insertFile = connection.CreateCommand())
        {
            insertFile.Transaction = transaction;
            insertFile.CommandText = """
                INSERT INTO platform.files
                    (id, tenant_id, storage_key, original_name, content_type, declared_content_type,
                     size_bytes, sha256, status, scan_status, uploaded_by, category, visibility,
                     project_id, entity_type, entity_id)
                VALUES
                    (@id, @tenantId, @storageKey, @name, @contentType, @contentType,
                     0, @sha256, 'pending_upload', 'not_scanned', @actorId, @category, @visibility,
                     @projectId, @entityType, @entityId);
                """;
            Add(insertFile, "id", fileId);
            Add(insertFile, "tenantId", tenantId);
            Add(insertFile, "storageKey", storageKey);
            Add(insertFile, "name", FilePolicy.SanitizeDownloadName(request.OriginalName));
            Add(insertFile, "contentType", request.ContentType);
            Add(insertFile, "sha256", (object?)request.Sha256 ?? DBNull.Value);
            Add(insertFile, "actorId", actorId);
            Add(insertFile, "category", (object?)request.Category ?? DBNull.Value);
            Add(insertFile, "visibility", request.Visibility ?? "internal");
            Add(insertFile, "projectId", (object?)request.ProjectId ?? DBNull.Value);
            Add(insertFile, "entityType", (object?)request.EntityType ?? DBNull.Value);
            Add(insertFile, "entityId", (object?)request.EntityId ?? DBNull.Value);
            await insertFile.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var insertSession = connection.CreateCommand())
        {
            insertSession.Transaction = transaction;
            insertSession.CommandText = """
                INSERT INTO platform.upload_sessions
                    (id, tenant_id, file_id, expected_size, expected_content_type,
                     expected_sha256, client_ref, expires_at)
                VALUES
                    (@id, @tenantId, @fileId, @size, @contentType, @sha256, @clientRef, @expiresAt);
                """;
            Add(insertSession, "id", sessionId);
            Add(insertSession, "tenantId", tenantId);
            Add(insertSession, "fileId", fileId);
            Add(insertSession, "size", request.SizeBytes);
            Add(insertSession, "contentType", request.ContentType);
            Add(insertSession, "sha256", (object?)request.Sha256 ?? DBNull.Value);
            Add(insertSession, "clientRef", (object?)request.ClientRef ?? DBNull.Value);
            Add(insertSession, "expiresAt", expiresAt);
            await insertSession.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new CreateUploadResponse(
            fileId,
            sessionId,
            presigned.Url,
            presigned.ExpiresAt,
            presigned.RequiredHeaders));
    }

    public async Task<Result<CompleteUploadResponse>> CompleteUploadAsync(
        Guid fileId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var row = await LoadAsync(connection, transaction, fileId, forUpdate: true, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<CompleteUploadResponse>(Error.NotFound("platform.file_not_found", "The file was not found."));
        }

        if (row.UploadedBy != actorId)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<CompleteUploadResponse>(Error.Forbidden("platform.file_access_denied", "Only the uploader can complete this upload."));
        }

        if (!string.Equals(row.Status, "pending_upload", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<CompleteUploadResponse>(Error.Conflict("platform.upload_incomplete", "The upload is not pending."));
        }

        if (row.SessionExpiresAt is null || row.SessionExpiresAt < _clock.GetUtcNow())
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<CompleteUploadResponse>(Error.Conflict("platform.upload_expired", "The upload session has expired."));
        }

        var stored = await _storage.HeadAsync(row.StorageKey, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<CompleteUploadResponse>(Error.Conflict("platform.upload_incomplete", "The upload has not been received by storage."));
        }

        if (row.ExpectedSize is { } expected && stored.SizeBytes != expected)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<CompleteUploadResponse>(Error.Conflict(
                "platform.file_size_mismatch",
                "The uploaded size does not match the declared size."));
        }

        if (stored.SizeBytes > FilePolicy.MaxBytesFor(row.Category))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<CompleteUploadResponse>(Error.Validation("platform.file_too_large", "The uploaded file exceeds the allowed size."));
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE platform.files
                SET status = 'scanning',
                    scan_status = 'pending',
                    size_bytes = @size,
                    sha256 = COALESCE(@sha256, sha256),
                    updated_at = now(),
                    row_version = row_version + 1
                WHERE id = @id;
                UPDATE platform.upload_sessions SET completed_at = now() WHERE file_id = @id;
                """;
            Add(update, "id", fileId);
            Add(update, "size", stored.SizeBytes);
            Add(update, "sha256", (object?)stored.Sha256 ?? DBNull.Value);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new CompleteUploadResponse(fileId, "scanning", "pending"));
    }

    public async Task<FileMetadataDto?> GetAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var row = await LoadAsync(connection, transaction, fileId, forUpdate: false, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return row is null || string.Equals(row.Status, "purged", StringComparison.Ordinal) ? null : ToDto(row);
    }

    public async Task<Result<FileDownloadResponse>> DownloadAsync(
        Guid fileId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var row = await LoadAsync(connection, transaction, fileId, forUpdate: false, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileDownloadResponse>(Error.NotFound("platform.file_not_found", "The file was not found."));
        }

        if (string.Equals(row.Status, "quarantined", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileDownloadResponse>(Error.Conflict("platform.file_quarantined", "The file is quarantined."));
        }

        if (!string.Equals(row.Status, "available", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileDownloadResponse>(Error.Conflict("platform.file_not_available", "The file is not available for download."));
        }

        if (row.EntityType is not null && row.EntityId is not null &&
            !await _access.CanReadLinkedRecordAsync(
                tenantId, row.EntityType, row.EntityId.Value, actorId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileDownloadResponse>(Error.NotFound("platform.file_not_found", "The file was not found."));
        }

        await using (var links = connection.CreateCommand())
        {
            links.Transaction = transaction;
            links.CommandText = """
                SELECT entity_type, entity_id FROM platform.file_links WHERE file_id = @fileId;
                """;
            Add(links, "fileId", fileId);

            await using var reader = await links.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var entityType = reader.GetString(0);
                var entityId = reader.GetGuid(1);

                if (!await _access.CanReadLinkedRecordAsync(
                        tenantId, entityType, entityId, actorId, cancellationToken).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Result.Failure<FileDownloadResponse>(Error.NotFound("platform.file_not_found", "The file was not found."));
                }
            }
        }

        var safeName = FilePolicy.SanitizeDownloadName(row.OriginalName);
        var download = await _storage.CreateDownloadAsync(
            row.StorageKey, DownloadLifetime, safeName, cancellationToken).ConfigureAwait(false);

        await _auditLog.WriteAsync(
            connection,
            transaction,
            new AuditEntry(
                tenantId,
                actorId,
                "file",
                fileId,
                "file.downloaded",
                Reason: null,
                IpAddress: ipAddress),
            cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new FileDownloadResponse(download.Url, download.ExpiresAt, safeName));
    }

    public async Task<Result<FileMetadataDto>> DeleteAsync(
        Guid fileId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var row = await LoadAsync(connection, transaction, fileId, forUpdate: true, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileMetadataDto>(Error.NotFound("platform.file_not_found", "The file was not found."));
        }

        if (row.LegalHold)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileMetadataDto>(Error.Conflict("platform.file_legal_hold", "The file is under legal hold."));
        }

        var postedLinks = await CountPostedFinancialLinksAsync(connection, transaction, fileId, cancellationToken)
            .ConfigureAwait(false);
        if (postedLinks > 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileMetadataDto>(Error.Conflict(
                "platform.file_in_use",
                "The file is linked to a posted financial record and cannot be deleted."));
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE platform.files
                SET status = 'deleted', deleted_at = now(), updated_at = now(), row_version = row_version + 1
                WHERE id = @id;
                """;
            Add(update, "id", fileId);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await _auditLog.WriteAsync(
            connection,
            transaction,
            new AuditEntry(tenantId, actorId, "file", fileId, "file.deleted", IpAddress: ipAddress),
            cancellationToken).ConfigureAwait(false);

        var updated = await LoadAsync(connection, transaction, fileId, forUpdate: false, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToDto(updated!));
    }

    public async Task<Result<FileLinkDto>> AddLinkAsync(
        Guid fileId,
        CreateFileLinkRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var row = await LoadAsync(connection, transaction, fileId, forUpdate: true, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileLinkDto>(Error.NotFound("platform.file_not_found", "The file was not found."));
        }

        if (string.Equals(row.Status, "quarantined", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileLinkDto>(Error.Conflict("platform.file_quarantined", "The file is quarantined."));
        }

        if (!string.Equals(row.Status, "available", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileLinkDto>(Error.Conflict("platform.file_not_available", "The file is not available."));
        }

        var id = Guid.CreateVersion7();

        try
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO platform.file_links
                    (id, tenant_id, file_id, entity_type, entity_id, link_role, caption)
                VALUES (@id, @tenantId, @fileId, @entityType, @entityId, @linkRole, @caption)
                ON CONFLICT (file_id, entity_type, entity_id) DO UPDATE SET caption = EXCLUDED.caption
                RETURNING id, created_at;
                """;
            Add(insert, "id", id);
            Add(insert, "tenantId", tenantId);
            Add(insert, "fileId", fileId);
            Add(insert, "entityType", request.EntityType);
            Add(insert, "entityId", request.EntityId);
            Add(insert, "linkRole", (object?)request.LinkRole ?? DBNull.Value);
            Add(insert, "caption", (object?)request.Caption ?? DBNull.Value);

            await using var reader = await insert.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            id = reader.GetGuid(0);
            var createdAt = reader.GetFieldValue<DateTimeOffset>(1);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(new FileLinkDto(
                id, fileId, request.EntityType, request.EntityId,
                request.LinkRole, request.Caption, createdAt));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileLinkDto>(Error.Conflict("platform.file_not_available", "The file is already linked."));
        }
    }

    public async Task<IReadOnlyList<FileLinkDto>> ListLinksAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id, file_id, entity_type, entity_id, link_role, caption, created_at
            FROM platform.file_links
            WHERE file_id = @fileId
            ORDER BY sort_order, created_at;
            """;
        Add(command, "fileId", fileId);

        var links = new List<FileLinkDto>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                links.Add(new FileLinkDto(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetGuid(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetFieldValue<DateTimeOffset>(6)));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return links;
    }

    public async Task<Result<FileMetadataDto>> ScanAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var row = await LoadAsync(connection, transaction, fileId, forUpdate: true, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<FileMetadataDto>(Error.NotFound("platform.file_not_found", "The file was not found."));
        }

        if (!string.Equals(row.Status, "scanning", StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToDto(row));
        }

        ScanResult result;
        string? detected = null;
        try
        {
            await using var content = await _storage.OpenReadAsync(row.StorageKey, cancellationToken)
                .ConfigureAwait(false);

            var header = new byte[16];
            var read = await content.ReadAsync(header, cancellationToken).ConfigureAwait(false);
            detected = FilePolicy.DetectContentType(header.AsSpan(0, read));

            content.Position = 0;
            result = await _scanner.ScanAsync(content, detected ?? row.ContentType, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = ScanResult.Unavailable;
        }

        var now = _clock.GetUtcNow();
        var magicMismatch = !FilePolicy.MagicBytesMatch(row.ContentType, detected);

        if (result.Verdict == ScanVerdict.Infected || magicMismatch)
        {
            await QuarantineAsync(connection, transaction, fileId,
                magicMismatch ? "type_mismatch" : result.ThreatName, cancellationToken).ConfigureAwait(false);
        }
        else if (result.Verdict == ScanVerdict.Clean)
        {
            await using var available = connection.CreateCommand();
            available.Transaction = transaction;
            available.CommandText = """
                UPDATE platform.files
                SET status = 'available', scan_status = 'clean', updated_at = now(), row_version = row_version + 1
                WHERE id = @id;
                """;
            Add(available, "id", fileId);
            await available.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (now - row.CreatedAt >= ScanTimeout)
        {
            await QuarantineAsync(connection, transaction, fileId, "scan_unavailable", cancellationToken)
                .ConfigureAwait(false);
        }

        var updated = await LoadAsync(connection, transaction, fileId, forUpdate: false, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToDto(updated!));
    }

    private static async Task QuarantineAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid fileId,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE platform.files
            SET status = 'quarantined',
                scan_status = CASE WHEN @reason = 'scan_unavailable' THEN 'unavailable' ELSE 'infected' END,
                scan_error = @reason,
                updated_at = now(),
                row_version = row_version + 1
            WHERE id = @id;
            """;
        Add(command, "id", fileId);
        Add(command, "reason", (object?)reason ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> CountPostedFinancialLinksAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid fileId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM platform.file_links
            WHERE file_id = @fileId
              AND (entity_type LIKE 'cost.%' OR entity_type LIKE 'cash.%'
                   OR entity_type LIKE 'procurement.payment%' OR entity_type LIKE 'contracts.invoice%'
                   OR entity_type LIKE 'contracts.certificate%');
            """;
        Add(command, "fileId", fileId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<SessionLookup?> FindSessionByClientRefAsync(
        DbConnection connection,
        DbTransaction transaction,
        string clientRef,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.id, s.file_id, s.expires_at
            FROM platform.upload_sessions s
            WHERE s.client_ref = @clientRef AND s.completed_at IS NULL AND s.expires_at > now()
            ORDER BY s.created_at DESC
            LIMIT 1;
            """;
        Add(command, "clientRef", clientRef);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new SessionLookup(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetFieldValue<DateTimeOffset>(2))
            : null;
    }

    private static async Task<FileRow?> LoadAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid fileId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT f.id, f.storage_key, f.original_name, f.content_type, f.size_bytes, f.sha256,
                   f.status, f.scan_status, f.uploaded_by, f.category, f.visibility, f.project_id,
                   f.entity_type, f.entity_id, f.legal_hold, f.row_version, f.created_at,
                   s.expected_size, s.expires_at
            FROM platform.files f
            LEFT JOIN platform.upload_sessions s ON s.file_id = f.id
            WHERE f.id = @id
            {(forUpdate ? "FOR UPDATE OF f" : string.Empty)};
            """;
        Add(command, "id", fileId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new FileRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetGuid(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetGuid(13),
            reader.GetBoolean(14),
            reader.GetInt32(15),
            reader.GetFieldValue<DateTimeOffset>(16),
            reader.IsDBNull(17) ? null : reader.GetInt64(17),
            reader.IsDBNull(18) ? null : reader.GetFieldValue<DateTimeOffset>(18));
    }

    private static FileMetadataDto ToDto(FileRow row) => new(
        row.Id,
        row.OriginalName,
        row.ContentType,
        row.SizeBytes,
        row.Sha256,
        row.Status,
        row.ScanStatus,
        row.Category,
        row.Visibility,
        row.ProjectId,
        row.LegalHold,
        row.RowVersion,
        row.CreatedAt);

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

    private static void Add(DbCommand command, string name, object value) =>
        command.Parameters.Add(new NpgsqlParameter(name, value));

    private sealed record SessionLookup(Guid SessionId, Guid FileId, DateTimeOffset ExpiresAt);

    private sealed record FileRow(
        Guid Id,
        string StorageKey,
        string OriginalName,
        string ContentType,
        long SizeBytes,
        string? Sha256,
        string Status,
        string ScanStatus,
        Guid UploadedBy,
        string? Category,
        string Visibility,
        Guid? ProjectId,
        string? EntityType,
        Guid? EntityId,
        bool LegalHold,
        int RowVersion,
        DateTimeOffset CreatedAt,
        long? ExpectedSize,
        DateTimeOffset? SessionExpiresAt);
}
