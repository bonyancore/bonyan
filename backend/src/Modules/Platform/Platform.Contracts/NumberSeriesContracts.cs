namespace Construction.Platform.Contracts;

public sealed record NumberSeriesDto(
    string Code,
    string? NameAr,
    string? NameEn,
    string Prefix,
    int Padding,
    string Scope,
    string Format,
    long StartValue,
    bool IsLocked,
    int RowVersion,
    string? NextPreview);

public sealed record UpdateNumberSeriesRequest(
    string? Format,
    int? Padding,
    long? StartAt,
    string? Reason);

public sealed record NumberAllocationDto(
    Guid Id,
    string Number,
    long Sequence,
    string ScopeKey,
    string? EntityType,
    Guid? EntityId,
    DateTimeOffset AllocatedAt);
