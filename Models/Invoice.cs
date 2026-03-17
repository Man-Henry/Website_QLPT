using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Website_QLPT.Models
{
    public enum InvoiceStatus
    {
        [Display(Name = "Chưa thu")]
        Unpaid = 0,
        [Display(Name = "Đã thu")]
        Paid = 1
    }

    public class Invoice
    {
        public int Id { get; set; }

        [Display(Name = "Kỳ thanh toán (Tháng/Năm)")]
        public int Month { get; set; }
        public int Year { get; set; }

        // Electricity
        [Display(Name = "Chỉ số điện đầu kỳ")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal ElectricityOld { get; set; }

        [Display(Name = "Chỉ số điện cuối kỳ")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal ElectricityNew { get; set; }

        [Display(Name = "Đơn giá điện (VND/kWh)")]
        [Column(TypeName = "decimal(10,0)")]
        public decimal ElectricityPrice { get; set; } = 3500;

        // Water
        [Display(Name = "Chỉ số nước đầu kỳ")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal WaterOld { get; set; }

        [Display(Name = "Chỉ số nước cuối kỳ")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal WaterNew { get; set; }

        [Display(Name = "Đơn giá nước (VND/m³)")]
        [Column(TypeName = "decimal(10,0)")]
        public decimal WaterPrice { get; set; } = 15000;

        // Room rent
        [Display(Name = "Tiền phòng tháng này (VND)")]
        [Column(TypeName = "decimal(18,0)")]
        public decimal RoomFee { get; set; }

        // Other fees
        [Display(Name = "Phí dịch vụ khác (VND)")]
        [Column(TypeName = "decimal(18,0)")]
        public decimal OtherFee { get; set; } = 0;

        [Display(Name = "Ghi chú phí khác")]
        public string? OtherFeeNote { get; set; }

        [Display(Name = "Trạng thái thanh toán")]
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;

        [Display(Name = "Ngày thu tiền")]
        public DateTime? PaidAt { get; set; }

        [Display(Name = "Ngày tạo hóa đơn")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Computed property – not stored in DB
        [NotMapped]
        [Display(Name = "Tiền điện")]
        public decimal ElectricityFee => (ElectricityNew - ElectricityOld) * ElectricityPrice;

        [NotMapped]
        [Display(Name = "Tiền nước")]
        public decimal WaterFee => (WaterNew - WaterOld) * WaterPrice;

        [NotMapped]
        [Display(Name = "Tổng cộng (VND)")]
        public decimal TotalAmount => RoomFee + ElectricityFee + WaterFee + OtherFee;

        // Foreign key
        [Required]
        public int ContractId { get; set; }

        // Navigation properties
        public virtual Contract? Contract { get; set; }
    }
}
