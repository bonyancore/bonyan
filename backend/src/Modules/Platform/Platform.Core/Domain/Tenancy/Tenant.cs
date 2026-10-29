using Construction.Kernel;

namespace Construction.Platform.Core.Domain.Tenancy;

public sealed class Tenant
{
    private Tenant() { }   // for EF

    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string? NameAr { get; private set; }
    public string? NameEn { get; private set; }
    public string Status { get; private set; } = "provisioning";
    public string PlanCode { get; private set; } = "starter";
    public string BaseCurrency { get; private set; } = "EGP";
    public string TimeZone { get; private set; } = "Africa/Cairo";
    public string DefaultLocale { get; private set; } = "ar";
    public string CountryCode { get; private set; } = "EG";
    public long PermissionsVersion { get; private set; } = 1;

    public static Tenant Create(Guid id, string code, string? nameAr, string? nameEn, string planCode)
    {
        if (string.IsNullOrWhiteSpace(nameAr) && string.IsNullOrWhiteSpace(nameEn))
            throw new DomainRuleViolation("platform.tenant_name_required");

        return new Tenant
        {
            Id = id,
            Code = code.ToLowerInvariant(),
            NameAr = nameAr,
            NameEn = nameEn,
            PlanCode = planCode,
        };
    }
}
