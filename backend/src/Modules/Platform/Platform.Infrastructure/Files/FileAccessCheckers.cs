using Construction.Platform.Core.Application.Files;

namespace Construction.Platform.Infrastructure.Files;

/// <summary>
/// Fail-closed record access checker used until PLT-106 delivers real project membership and
/// record-level rights. Downloads of linked files are denied rather than guessed.
/// </summary>
public sealed class DenyAllFileAccessChecker : IFileAccessChecker
{
    public Task<bool> CanReadLinkedRecordAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken cancellationToken = default) => Task.FromResult(false);
}

/// <summary>
/// Development/test-only checker. Production startup must never register this adapter.
/// </summary>
public sealed class FakeFileAccessChecker : IFileAccessChecker
{
    public Task<bool> CanReadLinkedRecordAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken cancellationToken = default) => Task.FromResult(true);
}
