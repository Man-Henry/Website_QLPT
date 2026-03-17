using Website_QLPT.Models;

namespace Website_QLPT.ViewModels
{
    public class ReportsIndexViewModel
    {
        public IReadOnlyList<PropertyRoomStatusReportItem> PropertyStatuses { get; set; } = [];
        public IReadOnlyList<Contract> ExpiringContracts { get; set; } = [];
    }

    public class PropertyRoomStatusReportItem
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public int TotalRooms { get; set; }
        public int AvailableRooms { get; set; }
        public int RentedRooms { get; set; }
        public int MaintenanceRooms { get; set; }
    }
}
