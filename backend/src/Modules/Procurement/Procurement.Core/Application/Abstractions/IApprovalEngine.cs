using Construction.Kernel.Tenancy;

namespace Construction.Procurement.Core.Application.Abstractions;

public interface IApprovalEngine
{
    Task<ApprovalDecision> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ApprovalRequest(
    Guid RecordId,
    string RecordType,
    string RecordNumber,
    Guid ProjectId,
    TenantId TenantId,
    decimal? Amount = null,
    string? Currency = null);

public sealed record ApprovalDecision(bool IsApproved, string Reference, string? RejectionReason = null);
