using System.ComponentModel.DataAnnotations;

namespace Website_QLPT.Models
{
    public class RoomImage
    {
        public int Id { get; set; }

        [Required]
        public string ImagePath { get; set; } = string.Empty; // relative path e.g. /uploads/rooms/abc.jpg

        [Display(Name = "Ảnh đại diện")]
        public bool IsThumbnail { get; set; } = false;

        [Display(Name = "Ngày upload")]
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        // Foreign key
        [Required]
        public int RoomId { get; set; }

        // Navigation property
        public virtual Room? Room { get; set; }
    }
}
