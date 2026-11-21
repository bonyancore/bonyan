namespace Construction.Web.Idempotency;

/// <summary>
/// Endpoint metadata that controls whether the idempotency middleware applies.
/// The convention marks every acting <c>POST</c> under <c>/api/v1</c> as <see cref="Optional"/>;
/// clients that identify themselves as mobile or portal must supply a key.
/// </summary>
public enum IdempotencyMode
{
    Disabled = 0,
    Optional = 1,
    Required = 2
}

public sealed record IdempotencyMetadata(IdempotencyMode Mode)
{
    public static IdempotencyMetadata Disabled { get; } = new(IdempotencyMode.Disabled);

    public static IdempotencyMetadata Optional { get; } = new(IdempotencyMode.Optional);

    public static IdempotencyMetadata Required { get; } = new(IdempotencyMode.Required);
}
