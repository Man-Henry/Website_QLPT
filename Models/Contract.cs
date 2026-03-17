using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Website_QLPT.Models
{
    public enum ContractStatus
    {
        [Display(Name = "Đang hiệu lực")]
        Active = 0,
        [Display(Name = "Đã hết hạn")]
        Expired = 1,
        [Display(Name = "Đã thanh lý")]
        Terminated = 2
    }

    public class Contract
    {
        public int Id { get; set; }

        [Display(Name = "Ngày bắt đầu")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "Ngày kết thúc")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Tiền cọc (VND)")]
        [Column(TypeName = "decimal(18,0)")]
        public decimal DepositAmount { get; set; }

        [Display(Name = "Trạng thái hợp đồng")]
        public ContractStatus Status { get; set; } = ContractStatus.Active;

        [Display(Name = "Ghi chú")]
        public string? Note { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Foreign keys
        [Required]
        public int RoomId { get; set; }

        [Required]
        public int TenantId { get; set; }

        // Navigation properties
        [Display(Name = "Phòng")]
        public virtual Room? Room { get; set; }

        [Display(Name = "Khách thuê")]
        public virtual Tenant? Tenant { get; set; }

        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}
