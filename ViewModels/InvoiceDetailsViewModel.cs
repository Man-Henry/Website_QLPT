using Website_QLPT.Models;

namespace Website_QLPT.ViewModels
{
    public class InvoiceDetailsViewModel
    {
        public required Invoice Invoice { get; set; }
        public bool IsAdmin { get; set; }
    }
}
