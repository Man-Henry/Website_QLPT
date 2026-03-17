using System.Security.Claims;
using Website_QLPT.Models;

namespace Website_QLPT.Services
{
    public interface ICurrentTenantService
    {
        Task<Tenant?> GetCurrentTenantAsync(ClaimsPrincipal principal);
        Task<Tenant?> TryAutoLinkAsync(string? identityUserId, string? email);
    }
}
