namespace Construction.Kernel;

/// <summary>Thrown when a domain invariant is broken. Code is a stable, translatable key, e.g. "platform.last_admin".</summary>
public sealed class DomainRuleViolation : Exception
{
    public DomainRuleViolation(string code) : base(code) => Code = code;
    public DomainRuleViolation(string code, string message) : base(message) => Code = code;
    public DomainRuleViolation(string code, string message, Exception inner) : base(message, inner) => Code = code;

    public string Code { get; }
}
