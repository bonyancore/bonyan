using Construction.Kernel;

namespace Construction.Platform.Core.Domain.Approvals;

public sealed record ApprovalStepSpec(
    int Order,
    string ApproverSource,
    IReadOnlyList<Guid> ApproverIds,
    decimal? LimitAmount);

/// <summary>Policy candidate as stored; the matcher is pure and independent of persistence.</summary>
public sealed record ApprovalPolicyCandidate(
    Guid Id,
    string Code,
    string RecordType,
    decimal MinAmount,
    decimal? MaxAmount,
    string Currency,
    Guid? ProjectId,
    string? Trade,
    int Priority,
    bool IsActive,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int Version,
    IReadOnlyList<ApprovalStepSpec> Steps);

public sealed record ApprovalMatchInput(
    string RecordType,
    decimal Amount,
    string Currency,
    Guid? ProjectId,
    string? Trade,
    DateOnly OnDate);

/// <summary>
/// Spec 01 BR-PLT-40…41: amount bands are [min, max); project specificity 4, trade specificity 2;
/// ties break by priority then code. Overlapping policies with equal specificity and priority are
/// rejected at save time.
/// </summary>
public static class ApprovalPolicyMatcher
{
    private static readonly string[] NeverSelfApprovable = ["payment", "payment_application"];

    public static Result<ApprovalPolicyCandidate> Match(
        IEnumerable<ApprovalPolicyCandidate> policies,
        ApprovalMatchInput input)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(input);

        var candidates = policies
            .Where(p => p.IsActive)
            .Where(p => string.Equals(p.RecordType, input.RecordType, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.Equals(p.Currency, input.Currency, StringComparison.OrdinalIgnoreCase))
            .Where(p => input.OnDate >= p.EffectiveFrom && (p.EffectiveTo is null || input.OnDate <= p.EffectiveTo))
            .Where(p => input.Amount >= p.MinAmount && (p.MaxAmount is null || input.Amount < p.MaxAmount))
            .Where(p => p.ProjectId is null || p.ProjectId == input.ProjectId)
            .Where(p => p.Trade is null || string.Equals(p.Trade, input.Trade, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Specificity)
            .ThenBy(p => p.Priority)
            .ThenBy(p => p.Code, StringComparer.Ordinal)
            .ToList();

        return candidates.Count == 0
            ? Result.Failure<ApprovalPolicyCandidate>(Error.Validation(
                "platform.approval_no_policy",
                "No approval policy matches this request."))
            : Result.Success(candidates[0]);
    }

    public static int Specificity(ApprovalPolicyCandidate policy) =>
        (policy.ProjectId is null ? 0 : 4) + (string.IsNullOrWhiteSpace(policy.Trade) ? 0 : 2);

    /// <summary>Rejects ambiguous overlapping policies at save time (BR-PLT-40 #5).</summary>
    public static Error? ValidateNoOverlap(
        IEnumerable<ApprovalPolicyCandidate> existing,
        ApprovalPolicyCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(candidate);

        foreach (var other in existing)
        {
            if (other.Id == candidate.Id || !other.IsActive)
            {
                continue;
            }

            if (!string.Equals(other.RecordType, candidate.RecordType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(other.Currency, candidate.Currency, StringComparison.OrdinalIgnoreCase) ||
                other.ProjectId != candidate.ProjectId ||
                !string.Equals(other.Trade, candidate.Trade, StringComparison.OrdinalIgnoreCase) ||
                other.Priority != candidate.Priority)
            {
                continue;
            }

            var amountsOverlap = candidate.MinAmount < (other.MaxAmount ?? decimal.MaxValue)
                && other.MinAmount < (candidate.MaxAmount ?? decimal.MaxValue);
            var datesOverlap = candidate.EffectiveFrom <= (other.EffectiveTo ?? DateOnly.MaxValue)
                && other.EffectiveFrom <= (candidate.EffectiveTo ?? DateOnly.MaxValue);

            if (amountsOverlap && datesOverlap)
            {
                return Error.Conflict(
                    "platform.approval_policy_overlap",
                    "The approval policy overlaps another policy with the same specificity and priority.");
            }
        }

        return null;
    }

    /// <summary>
    /// Segregation of duties: self-approval is denied by default. Payment record types can never
    /// permit it (BR-PLT-45 / D-208-02).
    /// </summary>
    public static Error? ValidateSelfApproval(
        string recordType,
        Guid requesterId,
        Guid actorId,
        bool tenantAllowsSelfApproval)
    {
        if (requesterId != actorId)
        {
            return null;
        }

        if (NeverSelfApprovable.Contains(recordType, StringComparer.OrdinalIgnoreCase))
        {
            return Error.Forbidden(
                "platform.approval_self_approval_denied",
                "Payment records never allow self-approval.");
        }

        return tenantAllowsSelfApproval
            ? null
            : Error.Forbidden(
                "platform.approval_self_approval_denied",
                "A requester cannot approve their own request.");
    }
}
