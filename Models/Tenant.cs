using System.ComponentModel.DataAnnotations;

namespace Website_QLPT.Models
{
    public class Tenant
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Họ và tên không được để trống.")]
        [Display(Name = "Họ và Tên")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Số CCCD/CMND")]
        [MaxLength(20)]
        public string? NationalId { get; set; }

        [Display(Name = "Số điện thoại")]
        [MaxLength(15)]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Ngày sinh")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Quê quán / Địa chỉ thường trú")]
        [MaxLength(300)]
        public string? HomeTown { get; set; }

        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string? Email { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(450)] // ASP.NET Core Identity uses nvarchar(450) for Id
        public string OwnerId { get; set; } = string.Empty;

        [MaxLength(450)]
        public string? IdentityUserId { get; set; }

        // Navigation properties
        public virtual Microsoft.AspNetCore.Identity.IdentityUser? IdentityUser { get; set; }
        public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    }
}
