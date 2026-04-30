using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Models
{
    public class WalletTransaction
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid WalletId { get; set; }

        [ForeignKey(nameof(WalletId))]
        public Wallet Wallet { get; set; } = null!;

        [Required]
        public WalletTxnType Type { get; set; }

        [Required]
        public WalletTxnStatus Status { get; set; } = WalletTxnStatus.Succeeded;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfter { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "EGP";

        [MaxLength(100)]
        public string? IdempotencyKey { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}