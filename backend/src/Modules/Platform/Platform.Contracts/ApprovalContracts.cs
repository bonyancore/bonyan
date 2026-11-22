namespace Construction.Platform.Contracts;

public sealed record ApprovalStepDto(
    int Order,
    string ApproverSource,
    IReadOnlyList<Guid> ApproverIds,
    decimal? LimitAmount);

public sealed record ApprovalPolicyDto(
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
    IReadOnlyList<ApprovalStepDto> Steps);

public sealed record CreateApprovalStepRequest(
    string ApproverSource,
    IReadOnlyList<Guid> ApproverIds,
    decimal? LimitAmount);

public sealed record CreateApprovalPolicyRequest(
    string Code,
    string RecordType,
    decimal MinAmount,
    decimal? MaxAmount,
    string Currency,
    Guid? ProjectId,
    string? Trade,
    int Priority,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    IReadOnlyList<CreateApprovalStepRequest> Steps,
    bool IsActive = true);

public sealed record CreateApprovalRequestRequest(
    string RecordType,
    Guid RecordId,
    Guid? ProjectId,
    decimal Amount,
    string Currency,
    string? Trade,
    string? Context,
    string? RecordVersion);

public sealed record ApprovalDecisionRequest(string? Comment);

public sealed record ApprovalInstanceDto(
    Guid Id,
    string RecordType,
    Guid RecordId,
    Guid? ProjectId,
    decimal Amount,
    string Currency,
    string Status,
    Guid RequestedBy,
    string PolicyCode,
    int PolicyVersion,
    int RowVersion,
    DateTimeOffset RequestedAt,
    IReadOnlyList<Guid> ApproverIds,
    string? ActiveStepStatus);

public sealed record ApprovalSimulationRequest(
    string RecordType,
    decimal Amount,
    string Currency,
    Guid? ProjectId,
    string? Trade,
    DateOnly? OnDate);

public sealed record ApprovalSimulationResponse(
    string? PolicyCode,
    int? PolicyVersion,
    decimal? Amount,
    IReadOnlyList<Guid> ApproverIds,
    string? Message);
