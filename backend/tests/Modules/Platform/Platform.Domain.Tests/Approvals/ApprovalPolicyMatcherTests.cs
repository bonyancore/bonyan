using Xunit;
using Construction.Platform.Core.Domain.Approvals;

namespace Construction.Platform.Domain.Tests.Approvals;

public sealed class ApprovalPolicyMatcherTests
{
    private static readonly DateOnly Today = new(2026, 11, 21);

    [Theory]
    [InlineData(99_999.99, "BAND-LOW")]
    [InlineData(100_000, "BAND-HIGH")]
    [InlineData(499_999.99, "BAND-HIGH")]
    [InlineData(500_000, null)]
    public void Match_uses_inclusive_minimum_and_exclusive_maximum(double amount, string? expected)
    {
        var policies = new[]
        {
            Policy("BAND-LOW", 0, 100_000m),
            Policy("BAND-HIGH", 100_000m, 500_000m)
        };

        var result = ApprovalPolicyMatcher.Match(
            policies,
            new ApprovalMatchInput("purchase_order", (decimal)amount, "EGP", null, null, Today));

        Assert.Equal(expected is not null, result.IsSuccess);
        if (expected is not null)
        {
            Assert.Equal(expected, result.Value.Code);
        }
    }

    [Fact]
    public void Match_prefers_project_specific_policy_over_tenant_wide()
    {
        var projectId = Guid.NewGuid();
        var policies = new[]
        {
            Policy("TENANT", 0, 1_000_000m),
            Policy("PROJECT", 0, 1_000_000m, projectId: projectId)
        };

        var result = ApprovalPolicyMatcher.Match(
            policies,
            new ApprovalMatchInput("purchase_order", 75_000m, "EGP", projectId, null, Today));

        Assert.Equal("PROJECT", result.Value.Code);
        Assert.Equal(4, ApprovalPolicyMatcher.Specificity(result.Value));
    }

    [Fact]
    public void Match_breaks_equal_specificity_by_priority_then_code()
    {
        var policies = new[]
        {
            Policy("B", 0, 1000m, priority: 10),
            Policy("A", 0, 1000m, priority: 10),
            Policy("C", 0, 1000m, priority: 5)
        };

        var result = ApprovalPolicyMatcher.Match(
            policies,
            new ApprovalMatchInput("purchase_order", 10m, "EGP", null, null, Today));

        Assert.Equal("C", result.Value.Code);
    }

    [Fact]
    public void Match_excludes_inactive_and_out_of_band_policies()
    {
        var policies = new[]
        {
            Policy("INACTIVE", 0, 1000m, active: false),
            Policy("LATE", 0, 1000m, effectiveFrom: Today.AddDays(1))
        };

        var result = ApprovalPolicyMatcher.Match(
            policies,
            new ApprovalMatchInput("purchase_order", 10m, "EGP", null, null, Today));

        Assert.True(result.IsFailure);
        Assert.Equal("platform.approval_no_policy", result.Error.Code);
    }

    [Fact]
    public void ValidateNoOverlap_rejects_same_specificity_and_priority()
    {
        var existing = Policy("EXISTING", 0, 500_000m);

        var error = ApprovalPolicyMatcher.ValidateNoOverlap(
            [existing], Policy("NEW", 100_000m, 600_000m));

        Assert.NotNull(error);
        Assert.Equal("platform.approval_policy_overlap", error.Code);
    }

    [Fact]
    public void ValidateNoOverlap_allows_disjoint_amount_bands()
    {
        var existing = Policy("EXISTING", 0, 100_000m);

        var error = ApprovalPolicyMatcher.ValidateNoOverlap(
            [existing], Policy("NEW", 100_000m, 200_000m));

        Assert.Null(error);
    }

    [Fact]
    public void ValidateSelfApproval_denies_requester_by_default()
    {
        var userId = Guid.NewGuid();

        var error = ApprovalPolicyMatcher.ValidateSelfApproval(
            "purchase_order", userId, userId, tenantAllowsSelfApproval: false);

        Assert.NotNull(error);
        Assert.Equal("platform.approval_self_approval_denied", error.Code);
    }

    [Fact]
    public void ValidateSelfApproval_allows_other_approver()
    {
        var error = ApprovalPolicyMatcher.ValidateSelfApproval(
            "purchase_order", Guid.NewGuid(), Guid.NewGuid(), tenantAllowsSelfApproval: false);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateSelfApproval_never_allows_payment()
    {
        var userId = Guid.NewGuid();

        var error = ApprovalPolicyMatcher.ValidateSelfApproval(
            "payment_application", userId, userId, tenantAllowsSelfApproval: true);

        Assert.NotNull(error);
    }

    private static ApprovalPolicyCandidate Policy(
        string code,
        decimal min,
        decimal max,
        Guid? projectId = null,
        int priority = 100,
        bool active = true,
        DateOnly? effectiveFrom = null) =>
        new(
            Guid.NewGuid(),
            code,
            "purchase_order",
            min,
            max,
            "EGP",
            projectId,
            null,
            priority,
            active,
            effectiveFrom ?? Today.AddDays(-30),
            null,
            1,
            [new ApprovalStepSpec(1, "project_manager", [Guid.NewGuid()], null)]);
}
