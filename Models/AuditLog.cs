using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Website_QLPT.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string OwnerId { get; set; } = null!; // Isolated per admin

        [Required]
        [StringLength(50)]
        public string ActionType { get; set; } = null!; // e.g. "Create", "Update", "Delete", "MarkAsPaid", "Terminate"

        [Required]
        [StringLength(100)]
        public string EntityName { get; set; } = null!; // e.g. "Invoice", "Contract", "Room"

        public int? EntityId { get; set; } // The ID of the affected record

        public string? Details { get; set; } // Description of the change

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
