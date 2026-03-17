using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Website_QLPT.Data;
using Website_QLPT.Models;
using Website_QLPT.ViewModels;
using ClosedXML.Excel;

namespace Website_QLPT.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Forbid();
            }

            var propertyStatuses = await _context.Properties
                .Where(p => p.OwnerId == ownerId)
                .OrderBy(p => p.Name)
                .Select(p => new PropertyRoomStatusReportItem
                {
                    PropertyId = p.Id,
                    PropertyName = p.Name,
                    TotalRooms = p.Rooms.Count,
                    AvailableRooms = p.Rooms.Count(r => r.Status == RoomStatus.Available),
                    RentedRooms = p.Rooms.Count(r => r.Status == RoomStatus.Rented),
                    MaintenanceRooms = p.Rooms.Count(r => r.Status == RoomStatus.Maintenance)
                })
                .ToListAsync();

            var expiringContracts = await _context.Contracts
                .Include(c => c.Room)
                    .ThenInclude(r => r!.Property)
                .Include(c => c.Tenant)
                .Where(c =>
                    c.Room!.Property!.OwnerId == ownerId
                    && c.Status == ContractStatus.Active
                    && c.EndDate.HasValue
                    && c.EndDate.Value.Date >= DateTime.Today
                    && c.EndDate.Value.Date <= DateTime.Today.AddDays(30))
                .OrderBy(c => c.EndDate)
                .ToListAsync();

            ViewData["Title"] = "Báo Cáo Thống Kê";
            return View(new ReportsIndexViewModel
            {
                PropertyStatuses = propertyStatuses,
                ExpiringContracts = expiringContracts
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportRevenue(DateTime fromDate, DateTime toDate)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Forbid();
            }
            
            // Validate dates
            if (toDate < fromDate)
            {
                TempData["Error"] = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.";
                return RedirectToAction(nameof(Index));
            }

            var fromDateValue = fromDate.Date;
            var toDateValue = toDate.Date.AddDays(1).AddTicks(-1);

            var invoices = await _context.Invoices
                .Include(i => i.Contract).ThenInclude(c => c!.Room).ThenInclude(r => r!.Property)
                .Include(i => i.Contract).ThenInclude(c => c!.Tenant)
                .Where(i => i.Contract!.Room!.Property!.OwnerId == ownerId 
                         && i.CreatedAt >= fromDateValue
                         && i.CreatedAt <= toDateValue)
                .OrderBy(i => i.CreatedAt)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Doanh Thu");
            
            // Header
            var currentRow = 1;
            worksheet.Cell(currentRow, 1).Value = "Kỳ báo cáo";
            worksheet.Cell(currentRow, 2).Value = $"{fromDateValue:dd/MM/yyyy} - {toDate.Date:dd/MM/yyyy}";
            worksheet.Range(1, 1, 1, 2).Style.Font.Bold = true;

            currentRow = 3;
            worksheet.Cell(currentRow, 1).Value = "Mã HĐ";
            worksheet.Cell(currentRow, 2).Value = "Ngày tạo";
            worksheet.Cell(currentRow, 3).Value = "Phòng";
            worksheet.Cell(currentRow, 4).Value = "Khu nhà";
            worksheet.Cell(currentRow, 5).Value = "Khách thuê";
            worksheet.Cell(currentRow, 6).Value = "Kỳ (Tháng/Năm)";
            worksheet.Cell(currentRow, 7).Value = "Tiền phòng";
            worksheet.Cell(currentRow, 8).Value = "Tiền điện";
            worksheet.Cell(currentRow, 9).Value = "Tiền nước";
            worksheet.Cell(currentRow, 10).Value = "Phí khác";
            worksheet.Cell(currentRow, 11).Value = "Tổng cộng";
            worksheet.Cell(currentRow, 12).Value = "Trạng thái";
            
            var headerRange = worksheet.Range(currentRow, 1, currentRow, 12);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.AirForceBlue;
            headerRange.Style.Font.FontColor = XLColor.White;

            // Data
            decimal totalRevenue = 0;
            decimal totalPaid = 0;
            decimal totalUnpaid = 0;

            foreach (var inv in invoices)
            {
                currentRow++;
                worksheet.Cell(currentRow, 1).Value = inv.Id;
                worksheet.Cell(currentRow, 2).Value = inv.CreatedAt.ToString("dd/MM/yyyy");
                worksheet.Cell(currentRow, 3).Value = inv.Contract?.Room?.Name;
                worksheet.Cell(currentRow, 4).Value = inv.Contract?.Room?.Property?.Name;
                worksheet.Cell(currentRow, 5).Value = inv.Contract?.Tenant?.FullName;
                worksheet.Cell(currentRow, 6).Value = $"T{inv.Month}/{inv.Year}";
                worksheet.Cell(currentRow, 7).Value = inv.RoomFee;
                worksheet.Cell(currentRow, 8).Value = inv.ElectricityFee;
                worksheet.Cell(currentRow, 9).Value = inv.WaterFee;
                worksheet.Cell(currentRow, 10).Value = inv.OtherFee;
                worksheet.Cell(currentRow, 11).Value = inv.TotalAmount;
                worksheet.Cell(currentRow, 12).Value = inv.Status == InvoiceStatus.Paid ? "Đã thu" : "Chưa thu";

                // Format numbers
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0";

                // Accumulate totals
                totalRevenue += inv.TotalAmount;
                if (inv.Status == InvoiceStatus.Paid) totalPaid += inv.TotalAmount;
                if (inv.Status == InvoiceStatus.Unpaid) totalUnpaid += inv.TotalAmount;
            }

            // Summary Footer
            currentRow += 2;
            worksheet.Cell(currentRow, 10).Value = "TỔNG DOANH THU:";
            worksheet.Cell(currentRow, 11).Value = totalRevenue;
            worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0";
            worksheet.Range(currentRow, 10, currentRow, 11).Style.Font.Bold = true;
            
            currentRow++;
            worksheet.Cell(currentRow, 10).Value = "ĐÃ THU:";
            worksheet.Cell(currentRow, 11).Value = totalPaid;
            worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0";
            worksheet.Range(currentRow, 10, currentRow, 11).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 11).Style.Font.FontColor = XLColor.Green;

            currentRow++;
            worksheet.Cell(currentRow, 10).Value = "CHƯA THU:";
            worksheet.Cell(currentRow, 11).Value = totalUnpaid;
            worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "#,##0";
            worksheet.Range(currentRow, 10, currentRow, 11).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 11).Style.Font.FontColor = XLColor.Red;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            
            string fileName = $"BaoCao_DoanhThu_{fromDateValue:yyyyMMdd}_{toDate.Date:yyyyMMdd}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
