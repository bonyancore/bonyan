using Construction.Kernel.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Construction.Api.Tenancy;

public sealed class ClaimsPrincipalTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private const string TenantClaimType = "tid";

    public bool HasTenant
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var claims = user.FindAll(TenantClaimType).ToList();
            if (claims.Count != 1)
            {
                return false;
            }

            var claimValue = claims[0].Value;
            return Guid.TryParse(claimValue, out var guid) && guid != Guid.Empty;
        }
    }

    public TenantId Current
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User
                ?? throw new InvalidOperationException("No active HttpContext or User principal.");

            if (user?.Identity?.IsAuthenticated != true)
            {
                throw new InvalidOperationException("Request is unauthenticated. No tenant context is available.");
            }

            var claims = user.FindAll(TenantClaimType).ToList();
            if (claims.Count == 0)
            {
                throw new InvalidOperationException("Authenticated principal does not contain a 'tid' claim.");
            }

            if (claims.Count > 1)
            {
                throw new InvalidOperationException("Security violation: Multiple 'tid' claims detected on authenticated principal.");
            }

            var claimValue = claims[0].Value;
            if (!Guid.TryParse(claimValue, out var guid) || guid == Guid.Empty)
            {
                throw new InvalidOperationException($"Security violation: Malformed or empty 'tid' claim value: '{claimValue}'.");
            }

            return TenantId.From(guid);
        }
    }
}

