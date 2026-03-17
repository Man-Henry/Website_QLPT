using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Website_QLPT.Models
{
    public enum RoomStatus
    {
        [Display(Name = "Đang trống")]
        Available = 0,
        [Display(Name = "Đã cho thuê")]
        Rented = 1,
        [Display(Name = "Đang bảo trì")]
        Maintenance = 2
    }

    public class Room
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên phòng không được để trống.")]
        [Display(Name = "Tên Phòng")]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Diện tích (m²)")]
        [Column(TypeName = "decimal(8,2)")]
        public decimal? Area { get; set; }

        [Required(ErrorMessage = "Giá thuê không được để trống.")]
        [Display(Name = "Giá thuê (VND/tháng)")]
        [Column(TypeName = "decimal(18,0)")]
        public decimal Price { get; set; }

        [Display(Name = "Trạng thái")]
        public RoomStatus Status { get; set; } = RoomStatus.Available;

        [Display(Name = "Ghi chú")]
        public string? Note { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Foreign key
        [Required]
        public int PropertyId { get; set; }

        // Navigation properties
        [Display(Name = "Khu nhà")]
        public virtual Property? Property { get; set; }

        public virtual ICollection<RoomImage> Images { get; set; } = new List<RoomImage>();
        public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    }
}
