using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Website_QLPT.Models
{
    public enum TicketStatus
    {
        [Display(Name = "Đã tiếp nhận")]
        Open,
        [Display(Name = "Đang xử lý")]
        InProgress,
        [Display(Name = "Hoàn thành")]
        Resolved,
        [Display(Name = "Đã hủy")]
        Closed
    }

    public enum TicketPriority
    {
        [Display(Name = "Thấp")]
        Low,
        [Display(Name = "Bình thường")]
        Medium,
        [Display(Name = "Cao")]
        High,
        [Display(Name = "Khẩn cấp")]
        Urgent
    }

    public class MaintenanceTicket
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề sự cố")]
        [StringLength(100)]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng mô tả chi tiết sự cố")]
        [Display(Name = "Mô tả chi tiết")]
        public string Description { get; set; } = null!;

        [Display(Name = "Mức độ ưu tiên")]
        public TicketPriority Priority { get; set; } = TicketPriority.Medium;

        [Display(Name = "Trạng thái")]
        public TicketStatus Status { get; set; } = TicketStatus.Open;

        [Display(Name = "Ngày gửi")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Ngày cập nhật")]
        public DateTime? UpdatedAt { get; set; }

        public string? ImagePath { get; set; }

        // Foreign keys back to Contract or Room to know where the issue is
        [Required]
        public int ContractId { get; set; }
        
        [ForeignKey("ContractId")]
        public Contract? Contract { get; set; }
    }
}
