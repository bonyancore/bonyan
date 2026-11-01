using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Construction.Api;

/// <summary>
/// Application-boundary authentication for the platform admin endpoints.
///
/// The real identity provider (bearer tokens, tenant + permission claims) is not part of PLT-202/203/204
/// yet. Without a registered scheme, every <c>RequireAuthorization</c> endpoint fails the challenge with
/// "Unable to find the required 'IAuthenticationService' service" and returns 500 instead of an
/// authorization result, so the dead-letter admin API could not be exercised at all.
///
/// Development: identity is taken from explicit headers so the endpoint matrix (401/403/200/409) can be
/// driven locally with curl. Production and any other environment: the handler never authenticates, so
/// the endpoints fail closed with 401 and no caller can synthesize a tenant or permission.
///
/// Replace later: swap this for the real bearer-token scheme when the authentication ticket lands.
/// </summary>
public sealed class DevHeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "PlatformDev";

    private const string UserIdHeader = "X-Dev-UserId";
    private const string TenantIdHeader = "X-Dev-TenantId";
    private const string PermissionsHeader = "X-Dev-Permissions";

    private readonly IHostEnvironment _environment;

    public DevHeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Fail closed everywhere except Development: no header-derived identity outside a local run.
        if (!_environment.IsDevelopment())
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers[UserIdHeader].ToString();
        var tenantId = Request.Headers[TenantIdHeader].ToString();
        if (!Guid.TryParse(userId, out _) || !Guid.TryParse(tenantId, out _))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new("tid", tenantId),
            new("tenant_id", tenantId)
        };

        foreach (var permission in Request.Headers[PermissionsHeader]
                     .ToString()
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim("permission", permission));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
