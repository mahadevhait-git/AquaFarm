using AquaFarm.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AquaFarm.Infrastructure.Data;

public class AquaFarmDbContext : DbContext
{
    public AquaFarmDbContext(DbContextOptions<AquaFarmDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Pond> Ponds => Set<Pond>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanRepayment> LoanRepayments => Set<LoanRepayment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(u => u.PhoneNumber).IsUnique();
        modelBuilder.Entity<AppUser>().HasMany(u => u.Ponds).WithOne(p => p.Owner).HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AppUser>().HasMany(u => u.LoansLent).WithOne(l => l.Lender).HasForeignKey(l => l.LenderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AppUser>().HasMany(u => u.LoansBorrowed).WithOne(l => l.Borrower).HasForeignKey(l => l.BorrowerId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Group>().HasOne(g => g.Manager).WithMany().HasForeignKey(g => g.ManagerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Pond>().HasOne(p => p.Group).WithMany(g => g.Ponds).HasForeignKey(p => p.GroupId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GroupMembership>().HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId);
        modelBuilder.Entity<GroupMembership>().HasOne(m => m.User).WithMany(u => u.GroupMemberships).HasForeignKey(m => m.UserId);

        modelBuilder.Entity<Transaction>().HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Transaction>().HasOne(t => t.Pond).WithMany(p => p.Transactions).HasForeignKey(t => t.PondId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Transaction>().HasOne(t => t.Group).WithMany(g => g.SharedTransactions).HasForeignKey(t => t.GroupId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Loan>().HasMany(l => l.Repayments).WithOne(r => r.Loan).HasForeignKey(r => r.LoanId);

        base.OnModelCreating(modelBuilder);
    }
}
