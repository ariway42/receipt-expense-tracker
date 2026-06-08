using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReceiptExpenseTracker.Models;
namespace ReceiptExpenseTracker.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TransactionItem> TransactionItems { get; set; }
        public DbSet<WaOtp> WaOtps { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                      .UseIdentityColumn();

                entity.Property(e => e.StoreName)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(e => e.TotalAmount)
                      .HasPrecision(12, 2);

                entity.Property(e => e.ReceiptImagePath)
                      .HasMaxLength(500);

                entity.Property(e => e.UserId).HasMaxLength(450);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(e => e.TransactionItems);

                entity.HasMany(e => e.TransactionItems)
                      .WithOne(e => e.Transaction)
                      .HasForeignKey(e => e.TransactionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TransactionItem>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                      .UseIdentityColumn();

                entity.Property(e => e.ItemName)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(e => e.Price)
                      .HasPrecision(12, 2);
            });

            modelBuilder.Entity<Transaction>()
                .HasIndex(e => e.TransactionDate);

            modelBuilder.Entity<Transaction>()
                .HasIndex(e => e.StoreName);

            modelBuilder.Entity<TransactionItem>()
                .HasIndex(e => e.TransactionId);
        }
    }
}