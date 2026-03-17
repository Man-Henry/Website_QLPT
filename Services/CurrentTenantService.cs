using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Website_QLPT.Data;
using Website_QLPT.Models;

namespace Website_QLPT.Services
{
    public class CurrentTenantService : ICurrentTenantService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CurrentTenantService> _logger;

        public CurrentTenantService(ApplicationDbContext context, ILogger<CurrentTenantService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public Task<Tenant?> GetCurrentTenantAsync(ClaimsPrincipal principal)
        {
            var identityUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.FindFirstValue(ClaimTypes.Email);
            return TryAutoLinkAsync(identityUserId, email);
        }

        public async Task<Tenant?> TryAutoLinkAsync(string? identityUserId, string? email)
        {
            if (string.IsNullOrWhiteSpace(identityUserId))
            {
                return null;
            }

            var linkedTenant = await _context.Tenants
                .Include(t => t.IdentityUser)
                .FirstOrDefaultAsync(t => t.IdentityUserId == identityUserId);

            if (linkedTenant != null)
            {
                return linkedTenant;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var normalizedEmail = email.Trim().ToUpperInvariant();
            var candidates = await _context.Tenants
                .Where(t => t.IdentityUserId == null
                    && t.Email != null
                    && t.Email.ToUpper() == normalizedEmail)
                .ToListAsync();

            if (candidates.Count != 1)
            {
                if (candidates.Count > 1)
                {
                    _logger.LogWarning(
                        "Cannot auto-link Identity user {IdentityUserId} because {Count} tenant records share email {Email}.",
                        identityUserId,
                        candidates.Count,
                        email);
                }

                return null;
            }

            var tenant = candidates[0];
            tenant.IdentityUserId = identityUserId;
            await _context.SaveChangesAsync();

            return tenant;
        }
    }
}
