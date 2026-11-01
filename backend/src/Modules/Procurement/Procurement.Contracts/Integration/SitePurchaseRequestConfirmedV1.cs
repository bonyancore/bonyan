namespace Construction.Procurement.Contracts.Integration;

/// <summary>
/// Temporary inbound contract until the Core team publishes its WorkPackage event contract.
/// Keep this adapter isolated so it can be replaced without changing the procurement domain.
/// </summary>
public sealed record SitePurchaseRequestConfirmedV1(
    Guid EventId,
    Guid TenantId,
    Guid ProjectId,
    Guid RequestedBy,
    Guid WorkPackageId,
    string WorkPackageCode,
    string WorkPackageName,
    IReadOnlyList<SitePurchaseRequestLineV1> Lines,
    DateTimeOffset ConfirmedAt);

public sealed record SitePurchaseRequestLineV1(string Description, decimal Quantity, string Unit);
