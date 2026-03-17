using Website_QLPT.Models;

namespace Website_QLPT.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalRooms { get; set; }
        public int AvailableRooms { get; set; }
        public int RentedRooms { get; set; }
        public int MaintenanceRooms { get; set; }
        public int TotalTenants { get; set; }
        public int ActiveContracts { get; set; }
        public int UnpaidInvoices { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int OccupancyRate { get; set; }
        public string RevenueLabelsJson { get; set; } = "[]";
        public string RevenueDataJson { get; set; } = "[]";
        public string RoomStatusDataJson { get; set; } = "[]";
        public IReadOnlyList<Contract> RecentContracts { get; set; } = [];
        public IReadOnlyList<Contract> UpcomingExpiryContracts { get; set; } = [];
        public IReadOnlyList<AuditLog> RecentLogs { get; set; } = [];
    }
}
