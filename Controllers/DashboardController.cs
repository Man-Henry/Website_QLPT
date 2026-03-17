using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Website_QLPT.Data;
using Website_QLPT.Models;
using Website_QLPT.ViewModels;

namespace Website_QLPT.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var totalRooms = await _context.Rooms.CountAsync(r => r.Property!.OwnerId == ownerId);
            var availableRooms = await _context.Rooms.CountAsync(r => r.Status == RoomStatus.Available && r.Property!.OwnerId == ownerId);
            var rentedRooms = await _context.Rooms.CountAsync(r => r.Status == RoomStatus.Rented && r.Property!.OwnerId == ownerId);
            
            var tenantsInContracts = await _context.Contracts
                .Where(c => c.Room!.Property!.OwnerId == ownerId)
                .Select(c => c.TenantId)
                .Distinct()
                .CountAsync();
            var totalTenants = tenantsInContracts;

            var activeContracts = await _context.Contracts.CountAsync(c => c.Status == ContractStatus.Active && c.Room!.Property!.OwnerId == ownerId);

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            var unpaidInvoicesCount = await _context.Invoices.CountAsync(
                i => i.Status == InvoiceStatus.Unpaid && i.Month == currentMonth && i.Year == currentYear && i.Contract!.Room!.Property!.OwnerId == ownerId);

            var recentContracts = await _context.Contracts
                .Include(c => c.Room).ThenInclude(r => r!.Property)
                .Include(c => c.Tenant)
                .Where(c => c.Status == ContractStatus.Active && c.Room!.Property!.OwnerId == ownerId)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .ToListAsync();

            var currentDate = DateTime.Now;
            var upcomingExpiryContracts = await _context.Contracts
                .Include(c => c.Room).ThenInclude(r => r!.Property)
                .Include(c => c.Tenant)
                .Where(c => c.Status == ContractStatus.Active 
                         && c.Room!.Property!.OwnerId == ownerId
                         && c.EndDate.HasValue 
                         && c.EndDate.Value <= currentDate.AddDays(30))
                .OrderBy(c => c.EndDate)
                .ToListAsync();

            // Revenue for last 6 months
            var sixMonthsAgo = DateTime.Now.AddMonths(-5);
            var recentInvoices = await _context.Invoices
                .Where(i => i.Contract!.Room!.Property!.OwnerId == ownerId 
                         && i.Status == InvoiceStatus.Paid
                         && i.CreatedAt >= new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1))
                .ToListAsync();

            var revenueData = new List<decimal>();
            var revenueLabels = new List<string>();

            for (int i = 5; i >= 0; i--)
            {
                var targetDate = DateTime.Now.AddMonths(-i);
                revenueLabels.Add($"T{targetDate.Month}/{targetDate.Year}");
                
                var total = recentInvoices
                    .Where(inv => inv.Month == targetDate.Month && inv.Year == targetDate.Year)
                    .Sum(inv => inv.TotalAmount);
                revenueData.Add(total);
            }

            var recentLogs = await _context.AuditLogs
                .Where(log => log.OwnerId == ownerId)
                .OrderByDescending(log => log.CreatedAt)
                .Take(8)
                .ToListAsync();

            ViewData["Title"] = "Dashboard";
            return View(new DashboardViewModel
            {
                TotalRooms = totalRooms,
                AvailableRooms = availableRooms,
                RentedRooms = rentedRooms,
                MaintenanceRooms = totalRooms - availableRooms - rentedRooms,
                TotalTenants = totalTenants,
                ActiveContracts = activeContracts,
                UnpaidInvoices = unpaidInvoicesCount,
                MonthlyRevenue = revenueData.LastOrDefault(),
                OccupancyRate = totalRooms > 0 ? (int)Math.Round((double)rentedRooms / totalRooms * 100) : 0,
                RevenueLabelsJson = System.Text.Json.JsonSerializer.Serialize(revenueLabels),
                RevenueDataJson = System.Text.Json.JsonSerializer.Serialize(revenueData),
                RoomStatusDataJson = System.Text.Json.JsonSerializer.Serialize(new[] { rentedRooms, availableRooms, totalRooms - availableRooms - rentedRooms }),
                RecentContracts = recentContracts,
                UpcomingExpiryContracts = upcomingExpiryContracts,
                RecentLogs = recentLogs
            });
        }
    }
}
