using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Website_QLPT.Data;
using Website_QLPT.Models;
using Website_QLPT.Services;
using Website_QLPT.ViewModels;
using X.PagedList;
using X.PagedList.Extensions;

namespace Website_QLPT.Controllers
{
    [Authorize(Roles = "Admin,Tenant")]
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSenderService _emailSender;
        private readonly ICurrentTenantService _currentTenantService;

        public InvoicesController(
            ApplicationDbContext context,
            IEmailSenderService emailSender,
            ICurrentTenantService currentTenantService)
        {
            _context = context;
            _emailSender = emailSender;
            _currentTenantService = currentTenantService;
        }

        private bool IsAdmin => User.IsInRole("Admin");

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(int? month, int? year, string? status, int? page)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Forbid();
            }

            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            var query = BuildInvoiceQuery()
                .Where(i => i.Month == selectedMonth
                    && i.Year == selectedYear
                    && i.Contract!.Room!.Property!.OwnerId == ownerId);

            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<InvoiceStatus>(status, true, out var invoiceStatus))
            {
                query = query.Where(i => i.Status == invoiceStatus);
            }

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var pageNumber = page ?? 1;
            var pageSize = 10;

            ViewData["Title"] = "Hóa Đơn";
            return View(new InvoiceIndexViewModel
            {
                Invoices = invoices.ToPagedList(pageNumber, pageSize),
                Month = selectedMonth,
                Year = selectedYear,
                Status = status,
                TotalCount = invoices.Count,
                TotalPaid = invoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.TotalAmount),
                TotalUnpaid = invoices.Where(i => i.Status == InvoiceStatus.Unpaid).Sum(i => i.TotalAmount)
            });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(int? contractId)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Forbid();
            }

            await PopulateContractSelectListAsync(ownerId, contractId);

            var invoice = new Invoice
            {
                Month = DateTime.Now.Month,
                Year = DateTime.Now.Year
            };

            if (contractId.HasValue)
            {
                var contract = await _context.Contracts
                    .Include(c => c.Room)
                    .ThenInclude(r => r!.Property)
                    .FirstOrDefaultAsync(c =>
                        c.Id == contractId.Value
                        && c.Status == ContractStatus.Active
                        && c.Room!.Property!.OwnerId == ownerId);

                if (contract?.Room != null)
                {
                    invoice.ContractId = contract.Id;
                    invoice.RoomFee = contract.Room.Price;
                }
            }

            ViewData["Title"] = "Tạo Hóa Đơn";
            return View(invoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("ContractId,Month,Year,ElectricityOld,ElectricityNew,ElectricityPrice,WaterOld,WaterNew,WaterPrice,RoomFee,OtherFee,OtherFeeNote")] Invoice invoice)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Forbid();
            }

            await ValidateInvoiceAsync(invoice, ownerId);

            if (ModelState.IsValid)
            {
                invoice.Status = InvoiceStatus.Unpaid;
                invoice.CreatedAt = DateTime.Now;
                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Đã tạo hóa đơn tháng {invoice.Month}/{invoice.Year}!";
                return RedirectToAction(nameof(Index), new { month = invoice.Month, year = invoice.Year });
            }

            await PopulateContractSelectListAsync(ownerId, invoice.ContractId);
            ViewData["Title"] = "Tạo Hóa Đơn";
            return View(invoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Forbid();
            }

            var invoice = await BuildInvoiceQuery()
                .FirstOrDefaultAsync(i =>
                    i.Id == id
                    && i.Contract!.Room!.Property!.OwnerId == ownerId);

            if (invoice == null)
            {
                return NotFound();
            }

            if (invoice.Status != InvoiceStatus.Paid)
            {
                invoice.Status = InvoiceStatus.Paid;
                invoice.PaidAt = DateTime.Now;

                _context.AuditLogs.Add(new AuditLog
                {
                    OwnerId = ownerId,
                    ActionType = "MarkAsPaid",
                    EntityName = "Invoice",
                    EntityId = invoice.Id,
                    Details = $"Đã thu {invoice.TotalAmount:N0} VND của phòng {invoice.Contract?.Room?.Name} (Kỳ T{invoice.Month}/{invoice.Year})"
                });

                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Đã đánh dấu hóa đơn là Đã thu!";
            return RedirectToAction(nameof(Index), new { month = invoice.Month, year = invoice.Year });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendEmailInvoice(int id)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Forbid();
            }

            var invoice = await BuildInvoiceQuery()
                .FirstOrDefaultAsync(i =>
                    i.Id == id
                    && i.Contract!.Room!.Property!.OwnerId == ownerId);

            if (invoice?.Contract?.Tenant == null || string.IsNullOrWhiteSpace(invoice.Contract.Tenant.Email))
            {
                TempData["Error"] = "Không thể gửi email vì khách thuê chưa có địa chỉ email hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var invoiceUrl = Url.Action("Print", "Invoices", new { id = invoice.Id }, protocol: Request.Scheme);
            var tenantEmail = invoice.Contract.Tenant.Email;
            var tenantName = invoice.Contract.Tenant.FullName;
            var roomName = invoice.Contract.Room?.Name;
            var periodName = $"T{invoice.Month}/{invoice.Year}";

            var subject = $"[QLPT] Thông báo hóa đơn tiền nhà - Phòng {roomName} ({periodName})";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; color: #333;'>
                    <h2 style='color: #1a3c5e;'>Thông Báo Hóa Đơn Tiền Nhà</h2>
                    <p>Xin chào <strong>{tenantName}</strong>,</p>
                    <p>Hệ thống Quản lý Phòng Trọ xin thông báo hóa đơn tiền nhà kỳ <strong>{periodName}</strong> của phòng <strong>{roomName}</strong> đã được lập.</p>
                    <div style='background: #f8fafc; padding: 15px; border-left: 4px solid #f97316; margin: 20px 0;'>
                        <p style='margin: 0; font-size: 16px;'>Tổng số tiền cần thanh toán: <strong style='color: #ef4444; font-size: 18px;'>{invoice.TotalAmount:N0} VNĐ</strong></p>
                    </div>
                    <p>Trạng thái hiện tại: <strong>{(invoice.Status == InvoiceStatus.Paid ? "Đã thanh toán" : "Chưa thanh toán")}</strong></p>
                    <p>Vui lòng click vào nút bên dưới để xem chi tiết hoặc in hóa đơn:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{invoiceUrl}' style='background: #f97316; color: white; text-decoration: none; padding: 12px 25px; border-radius: 5px; font-weight: bold; display: inline-block;'>Xem Chi Tiết Hóa Đơn</a>
                    </div>
                    <p style='font-size: 12px; color: #666; border-top: 1px solid #eee; padding-top: 10px;'>
                        Email này được gửi tự động. Vui lòng không trả lời. Cảm ơn bạn!
                    </p>
                </div>";

            try
            {
                await _emailSender.SendEmailAsync(tenantEmail, subject, body);

                _context.AuditLogs.Add(new AuditLog
                {
                    OwnerId = ownerId,
                    ActionType = "SendEmail",
                    EntityName = "Invoice",
                    EntityId = invoice.Id,
                    Details = $"Đã gửi email nhắc phí phòng {roomName} ({periodName}) đến {tenantEmail}"
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã gửi email hóa đơn {periodName} cho khách thuê {tenantName}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi gửi email: {ex.Message}";
            }

            return RedirectToAction(nameof(Index), new { month = invoice.Month, year = invoice.Year });
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            if (!IsAdmin)
            {
                var tenant = await _currentTenantService.GetCurrentTenantAsync(User);
                if (tenant == null)
                {
                    TempData["Error"] = "Tài khoản của bạn chưa được liên kết với hồ sơ người thuê.";
                    return RedirectToAction("Index", "TenantDashboard");
                }
            }

            var invoice = await GetAccessibleInvoiceAsync(id.Value);
            if (invoice == null)
            {
                return NotFound();
            }

            ViewData["Title"] = $"Hóa đơn tháng {invoice.Month}/{invoice.Year}";
            return View(new InvoiceDetailsViewModel
            {
                Invoice = invoice,
                IsAdmin = IsAdmin
            });
        }

        public async Task<IActionResult> Print(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            if (!IsAdmin)
            {
                var tenant = await _currentTenantService.GetCurrentTenantAsync(User);
                if (tenant == null)
                {
                    TempData["Error"] = "Tài khoản của bạn chưa được liên kết với hồ sơ người thuê.";
                    return RedirectToAction("Index", "TenantDashboard");
                }
            }

            var invoice = await GetAccessibleInvoiceAsync(id.Value);
            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportExcel(int? month, int? year, string? status)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Forbid();
            }

            var selectedMonth = month ?? DateTime.Now.Month;
            var selectedYear = year ?? DateTime.Now.Year;

            var query = BuildInvoiceQuery()
                .Where(i => i.Month == selectedMonth
                    && i.Year == selectedYear
                    && i.Contract!.Room!.Property!.OwnerId == ownerId);

            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<InvoiceStatus>(status, true, out var invoiceStatus))
            {
                query = query.Where(i => i.Status == invoiceStatus);
            }

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"Hóa Đơn T{selectedMonth}-{selectedYear}");

            var currentRow = 1;
            worksheet.Cell(currentRow, 1).Value = "Mã HĐ";
            worksheet.Cell(currentRow, 2).Value = "Phòng";
            worksheet.Cell(currentRow, 3).Value = "Khách thuê";
            worksheet.Cell(currentRow, 4).Value = "Kỳ";
            worksheet.Cell(currentRow, 5).Value = "Tiền phòng";
            worksheet.Cell(currentRow, 6).Value = "Tiền điện";
            worksheet.Cell(currentRow, 7).Value = "Tiền nước";
            worksheet.Cell(currentRow, 8).Value = "Phí khác";
            worksheet.Cell(currentRow, 9).Value = "Tổng cộng";
            worksheet.Cell(currentRow, 10).Value = "Trạng thái";
            worksheet.Range(1, 1, 1, 10).Style.Font.Bold = true;
            worksheet.Range(1, 1, 1, 10).Style.Fill.BackgroundColor = XLColor.LightGray;

            foreach (var invoice in invoices)
            {
                currentRow++;
                worksheet.Cell(currentRow, 1).Value = invoice.Id;
                worksheet.Cell(currentRow, 2).Value = invoice.Contract?.Room?.Name;
                worksheet.Cell(currentRow, 3).Value = invoice.Contract?.Tenant?.FullName;
                worksheet.Cell(currentRow, 4).Value = $"T{invoice.Month}/{invoice.Year}";
                worksheet.Cell(currentRow, 5).Value = invoice.RoomFee;
                worksheet.Cell(currentRow, 6).Value = invoice.ElectricityFee;
                worksheet.Cell(currentRow, 7).Value = invoice.WaterFee;
                worksheet.Cell(currentRow, 8).Value = invoice.OtherFee;
                worksheet.Cell(currentRow, 9).Value = invoice.TotalAmount;
                worksheet.Cell(currentRow, 10).Value = invoice.Status == InvoiceStatus.Paid ? "Đã thu" : "Chưa thu";

                worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0";
                worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0";
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"HoaDon_T{selectedMonth}_{selectedYear}.xlsx");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AuditLog(int? page)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Forbid();
            }

            var pageNumber = page ?? 1;
            const int pageSize = 20;

            var logs = await _context.AuditLogs
                .Where(a => a.OwnerId == ownerId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewData["Title"] = "Lịch Sử Hoạt Động";
            return View(logs.ToPagedList(pageNumber, pageSize));
        }

        private IQueryable<Invoice> BuildInvoiceQuery()
        {
            return _context.Invoices
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Room)
                        .ThenInclude(r => r!.Property)
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Tenant);
        }

        private async Task<Invoice?> GetAccessibleInvoiceAsync(int id)
        {
            var query = BuildInvoiceQuery().Where(i => i.Id == id);

            if (IsAdmin)
            {
                var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(ownerId))
                {
                    return null;
                }

                query = query.Where(i => i.Contract!.Room!.Property!.OwnerId == ownerId);
            }
            else
            {
                var tenant = await _currentTenantService.GetCurrentTenantAsync(User);
                if (tenant == null)
                {
                    return null;
                }

                query = query.Where(i => i.Contract!.TenantId == tenant.Id);
            }

            return await query.FirstOrDefaultAsync();
        }

        private async Task PopulateContractSelectListAsync(string ownerId, int? selectedContractId)
        {
            var activeContracts = await _context.Contracts
                .Include(c => c.Room)
                    .ThenInclude(r => r!.Property)
                .Include(c => c.Tenant)
                .Where(c => c.Status == ContractStatus.Active && c.Room!.Property!.OwnerId == ownerId)
                .OrderBy(c => c.Room!.Property!.Name)
                .ThenBy(c => c.Room!.Name)
                .ToListAsync();

            ViewBag.ContractId = new SelectList(
                activeContracts.Select(c => new
                {
                    c.Id,
                    Name = $"{c.Room?.Property?.Name} - {c.Room?.Name} | {c.Tenant?.FullName}"
                }),
                "Id",
                "Name",
                selectedContractId);
        }

        private async Task ValidateInvoiceAsync(Invoice invoice, string ownerId)
        {
            if (invoice.Month < 1 || invoice.Month > 12)
            {
                ModelState.AddModelError(nameof(Invoice.Month), "Tháng thanh toán phải nằm trong khoảng 1-12.");
            }

            if (invoice.Year < 2000 || invoice.Year > 2100)
            {
                ModelState.AddModelError(nameof(Invoice.Year), "Năm thanh toán không hợp lệ.");
            }

            if (invoice.ElectricityNew < invoice.ElectricityOld)
            {
                ModelState.AddModelError(nameof(Invoice.ElectricityNew), "Chỉ số điện cuối kỳ không được nhỏ hơn đầu kỳ.");
            }

            if (invoice.WaterNew < invoice.WaterOld)
            {
                ModelState.AddModelError(nameof(Invoice.WaterNew), "Chỉ số nước cuối kỳ không được nhỏ hơn đầu kỳ.");
            }

            var contract = await _context.Contracts
                .Include(c => c.Room)
                    .ThenInclude(r => r!.Property)
                .FirstOrDefaultAsync(c =>
                    c.Id == invoice.ContractId
                    && c.Status == ContractStatus.Active
                    && c.Room!.Property!.OwnerId == ownerId);

            if (contract == null)
            {
                ModelState.AddModelError(nameof(Invoice.ContractId), "Hợp đồng không hợp lệ hoặc không thuộc quyền quản lý của bạn.");
                return;
            }

            if (invoice.RoomFee <= 0 && contract.Room != null)
            {
                invoice.RoomFee = contract.Room.Price;
            }

            var duplicateExists = await _context.Invoices.AnyAsync(i =>
                i.ContractId == invoice.ContractId
                && i.Month == invoice.Month
                && i.Year == invoice.Year);

            if (duplicateExists)
            {
                ModelState.AddModelError(nameof(Invoice.ContractId), "Hóa đơn cho hợp đồng và kỳ thanh toán này đã tồn tại.");
            }
        }
    }
}
