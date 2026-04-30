using Microsoft.EntityFrameworkCore;

namespace DataLayer.Models
{
    public class DBContext : DbContext
    {
        public DBContext() : base()
        {
        }

        public DBContext(DbContextOptions<DBContext> options) : base(options)
        {
        }

        // ── Existing ──────────────────────────────────────────────────────
        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }

        // ── New ───────────────────────────────────────────────────────────
        public DbSet<FoundItem> FoundItems { get; set; }
        public DbSet<ClaimAttempt> ClaimAttempts { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── FoundItem ─────────────────────────────────────────────────
            // SQL Server forbids multiple cascade/set-null paths from the
            // same parent table. Use Restrict (NO ACTION) on every FK
            // that points from FoundItems back to Users.

            modelBuilder.Entity<FoundItem>()
                .HasOne(f => f.ReportedByUser)
                .WithMany()
                .HasForeignKey(f => f.ReportedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FoundItem>()
                .HasOne(f => f.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(f => f.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FoundItem>()
                .HasOne(f => f.ClaimedByUser)
                .WithMany()
                .HasForeignKey(f => f.ClaimedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── ClaimAttempt ──────────────────────────────────────────────
            modelBuilder.Entity<ClaimAttempt>()
                .HasIndex(c => new { c.FoundItemId, c.ClaimantUserId })
                .IsUnique();

            modelBuilder.Entity<ClaimAttempt>()
                .HasOne(c => c.FoundItem)
                .WithMany(f => f.ClaimAttempts)
                .HasForeignKey(c => c.FoundItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ClaimAttempt>()
                .HasOne(c => c.ClaimantUser)
                .WithMany()
                .HasForeignKey(c => c.ClaimantUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── ChatMessage ───────────────────────────────────────────────
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.FoundItem)
                .WithMany(f => f.ChatMessages)
                .HasForeignKey(m => m.FoundItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Recipient)
                .WithMany()
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Notification ──────────────────────────────────────────────
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}