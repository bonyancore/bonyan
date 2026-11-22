using Construction.Kernel;
using Construction.Platform.Contracts;

namespace Construction.Platform.Core.Application.Approvals;

public sealed record ApprovalResolutionRequest(
    Guid TenantId,
    string RecordType,
    Guid RecordId,
    Guid? ProjectId,
    decimal Amount,
    string Currency,
    IReadOnlyList<string> Roles);

public sealed record ApproverCandidate(Guid UserId, string Role, bool IsActive = true);

/// <summary>
/// Approver resolution port. A real implementation depends on PLT-106 (project membership/roles);
/// the fake used in Development/tests is explicitly marked and must not run in Production.
/// </summary>
public interface IApproverDirectory
{
    Task<IReadOnlyList<ApproverCandidate>> ResolveAsync(
        ApprovalResolutionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IApprovalService
{
    Task<Result<ApprovalInstanceDto>> RequestAsync(
        CreateApprovalRequestRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ApprovalInstanceDto?> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);

    Task<Result<ApprovalInstanceDto>> DecideAsync(
        Guid instanceId,
        bool approve,
        string? comment,
        int expectedRowVersion,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ApprovalSimulationResponse> SimulateAsync(
        ApprovalSimulationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalPolicyDto>> ListPoliciesAsync(CancellationToken cancellationToken = default);

    Task<Result<ApprovalPolicyDto>> CreatePolicyAsync(
        CreateApprovalPolicyRequest request,
        CancellationToken cancellationToken = default);
}
