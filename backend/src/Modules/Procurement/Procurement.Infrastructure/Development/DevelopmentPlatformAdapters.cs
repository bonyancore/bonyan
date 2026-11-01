using System.Threading;
using Construction.Kernel.Tenancy;
using Construction.Procurement.Core.Application.Abstractions;

namespace Construction.Procurement.Infrastructure.Development;

/// <summary>Temporary development adapter for PLT-206. Replace with the numbering service contract when ready.</summary>
public sealed class DevelopmentProcurementNumberGenerator : IPurchaseRequestNumberGenerator, IPurchaseOrderNumberGenerator
{
    private static long _requestSequence;
    private static long _orderSequence;

    public Task<string> GenerateAsync(TenantId tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult($"PR-DEV-{DateTime.UtcNow:yyyyMMdd}-{Interlocked.Increment(ref _requestSequence):D6}");
    }

    Task<string> IPurchaseOrderNumberGenerator.GenerateAsync(TenantId tenantId, Guid projectId, CancellationToken cancellationToken) 
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult($"PO-DEV-{DateTime.UtcNow:yyyyMMdd}-{Interlocked.Increment(ref _orderSequence):D6}");
    }
}

/// <summary>Temporary development adapter for PLT-208. Approves automatically and marks the result clearly as a mock.</summary>
public sealed class DevelopmentApprovalEngine : IApprovalEngine
{
    private static long _sequence;

    public Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reference = $"PLT208-DEV-{Interlocked.Increment(ref _sequence):D6}";
        return Task.FromResult(new ApprovalDecision(true, reference));
    }
}
