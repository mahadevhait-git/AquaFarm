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
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<PondBill> PondBills => Set<PondBill>();
    public DbSet<ExpenseBill> ExpenseBills => Set<ExpenseBill>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<CapitalTransaction> CapitalTransactions => Set<CapitalTransaction>();
    public DbSet<ContributionPayout> ContributionPayouts => Set<ContributionPayout>();
    public DbSet<LoanRepayment> LoanRepayments => Set<LoanRepayment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(u => u.PhoneNumber).IsUnique();
        modelBuilder.Entity<AppUser>().HasMany(u => u.Ponds).WithOne(p => p.Owner).HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AppUser>().HasMany(u => u.LoansLent).WithOne(l => l.Lender).HasForeignKey(l => l.LenderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AppUser>().HasMany(u => u.LoansBorrowed).WithOne(l => l.Borrower).HasForeignKey(l => l.BorrowerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AppUser>().HasMany(u => u.CapitalTransactions).WithOne(c => c.Farmer).HasForeignKey(c => c.FarmerId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Group>().HasOne(g => g.Manager).WithMany().HasForeignKey(g => g.ManagerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Pond>().HasOne(p => p.Group).WithMany(g => g.Ponds).HasForeignKey(p => p.GroupId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GroupMembership>().HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId);
        modelBuilder.Entity<GroupMembership>().HasOne(m => m.User).WithMany(u => u.GroupMemberships).HasForeignKey(m => m.UserId);

        modelBuilder.Entity<Transaction>().HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Transaction>().HasOne(t => t.Pond).WithMany(p => p.Transactions).HasForeignKey(t => t.PondId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Transaction>().HasOne(t => t.Group).WithMany(g => g.SharedTransactions).HasForeignKey(t => t.GroupId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Expense>().HasOne(e => e.Pond).WithMany().HasForeignKey(e => e.PondId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Expense>().HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Expense>().HasIndex(e => new { e.PondId, e.ExpenseDate });
        modelBuilder.Entity<Expense>().HasIndex(e => new { e.CreatedById, e.ExpenseDate });
        modelBuilder.Entity<PondBill>().HasOne(b => b.Pond).WithMany().HasForeignKey(b => b.PondId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PondBill>().HasOne(b => b.UploadedBy).WithMany().HasForeignKey(b => b.UploadedById).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PondBill>().HasIndex(b => new { b.PondId, b.UploadedAt });
        modelBuilder.Entity<ExpenseBill>().HasOne(b => b.Expense).WithMany().HasForeignKey(b => b.ExpenseId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ExpenseBill>().HasOne(b => b.UploadedBy).WithMany().HasForeignKey(b => b.UploadedById).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExpenseBill>().HasIndex(b => new { b.ExpenseId, b.UploadedAt });

        modelBuilder.Entity<Loan>().HasOne(l => l.Group).WithMany().HasForeignKey(l => l.GroupId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Loan>()
            .HasIndex(l => new { l.GroupId, l.BorrowerId })
            .HasDatabaseName("UX_Loans_GroupId_BorrowerId_GroupTotals")
            .HasFilter("[GroupId] IS NOT NULL")
            .IsUnique();
        modelBuilder.Entity<Loan>().HasMany(l => l.Repayments).WithOne(r => r.Loan).HasForeignKey(r => r.LoanId);

        modelBuilder.Entity<CapitalTransaction>().HasOne(c => c.Group).WithMany(g => g.CapitalTransactions).HasForeignKey(c => c.GroupId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CapitalTransaction>().HasOne(c => c.Farmer).WithMany(u => u.CapitalTransactions).HasForeignKey(c => c.FarmerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CapitalTransaction>().HasIndex(c => new { c.GroupId, c.FarmerId, c.ContributionDate });
        modelBuilder.Entity<ContributionPayout>().HasOne(p => p.Pond).WithMany().HasForeignKey(p => p.PondId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContributionPayout>().HasOne(p => p.Group).WithMany().HasForeignKey(p => p.GroupId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContributionPayout>().HasOne(p => p.Farmer).WithMany(u => u.ContributionPayoutsAsFarmer).HasForeignKey(p => p.FarmerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContributionPayout>().HasOne(p => p.Manager).WithMany(u => u.ContributionPayoutsAsManager).HasForeignKey(p => p.ManagerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContributionPayout>().HasOne(p => p.CapitalTransaction).WithMany(c => c.Payouts).HasForeignKey(p => p.CapitalTransactionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContributionPayout>().HasIndex(p => p.CapitalTransactionId).IsUnique();
        modelBuilder.Entity<ContributionPayout>().HasIndex(p => new { p.PondId, p.FarmerId, p.Status });
        modelBuilder.Entity<ContributionPayout>().HasIndex(p => new { p.FarmerId, p.CreatedAt });

        base.OnModelCreating(modelBuilder);
    }
}
