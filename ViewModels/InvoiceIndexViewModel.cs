using Website_QLPT.Models;
using X.PagedList;

namespace Website_QLPT.ViewModels
{
    public class InvoiceIndexViewModel
    {
        public required IPagedList<Invoice> Invoices { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string? Status { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalUnpaid { get; set; }
    }
}
