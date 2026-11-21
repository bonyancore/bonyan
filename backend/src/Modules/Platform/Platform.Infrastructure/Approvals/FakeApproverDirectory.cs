using Construction.Platform.Core.Application.Approvals;

namespace Construction.Platform.Infrastructure.Approvals;

/// <summary>
/// Development/test approver directory. It resolves nothing, so the service falls back to the
/// approver IDs configured on the policy step. Production must register a real adapter backed by
/// PLT-106 membership; this type must never be registered outside Development/Test.
/// </summary>
public sealed class FakeApproverDirectory : IApproverDirectory
{
    public Task<IReadOnlyList<ApproverCandidate>> ResolveAsync(
        ApprovalResolutionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ApproverCandidate>>([]);
}
