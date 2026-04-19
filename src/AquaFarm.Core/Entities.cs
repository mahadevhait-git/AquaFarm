using System.ComponentModel.DataAnnotations;

namespace AquaFarm.Core.Entities;

public class AppUser
{
    [Key]
    public Guid Id { get; set; }
    public string UserName { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.Farmer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Pond> Ponds { get; set; } = new List<Pond>();
    public ICollection<GroupMembership> GroupMemberships { get; set; } = new List<GroupMembership>();
    public ICollection<Loan> LoansLent { get; set; } = new List<Loan>();
    public ICollection<Loan> LoansBorrowed { get; set; } = new List<Loan>();
    public ICollection<CapitalTransaction> CapitalTransactions { get; set; } = new List<CapitalTransaction>();
}

public class Pond
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Location { get; set; }
    public Guid OwnerId { get; set; }
    public AppUser? Owner { get; set; }
    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

public class Group
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ManagerId { get; set; }
    public AppUser? Manager { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<GroupMembership> Members { get; set; } = new List<GroupMembership>();
    public ICollection<Pond> Ponds { get; set; } = new List<Pond>();
    public ICollection<Transaction> SharedTransactions { get; set; } = new List<Transaction>();
    public ICollection<CapitalTransaction> CapitalTransactions { get; set; } = new List<CapitalTransaction>();
}

public class GroupMembership
{
    [Key]
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Group? Group { get; set; }
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public MembershipRole Role { get; set; } = MembershipRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class Transaction
{
    [Key]
    public Guid Id { get; set; }
    public TransactionType Type { get; set; }
    public string Category { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public Guid? PondId { get; set; }
    public Pond? Pond { get; set; }
    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }
    public Guid CreatedById { get; set; }
    public AppUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Expense
{
    [Key]
    public Guid Id { get; set; }
    public Guid PondId { get; set; }
    public Pond? Pond { get; set; }
    public decimal Amount { get; set; }
    public string Purpose { get; set; } = default!;
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    public string? BillFileName { get; set; }
    public string? BillContentType { get; set; }
    public string? BillStoragePath { get; set; }
    public Guid CreatedById { get; set; }
    public AppUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PondBill
{
    [Key]
    public Guid Id { get; set; }
    public Guid PondId { get; set; }
    public Pond? Pond { get; set; }
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string StoragePath { get; set; } = default!;
    public Guid UploadedById { get; set; }
    public AppUser? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class ExpenseBill
{
    [Key]
    public Guid Id { get; set; }
    public Guid ExpenseId { get; set; }
    public Expense? Expense { get; set; }
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string StoragePath { get; set; } = default!;
    public Guid UploadedById { get; set; }
    public AppUser? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class Loan
{
    [Key]
    public Guid Id { get; set; }
    public Guid LenderId { get; set; }
    public AppUser? Lender { get; set; }
    public Guid BorrowerId { get; set; }
    public AppUser? Borrower { get; set; }
    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public InterestType InterestType { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public int TermMonths { get; set; }
    public decimal OutstandingBalance { get; set; }
    public bool IsClosed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<LoanRepayment> Repayments { get; set; } = new List<LoanRepayment>();
}

public class CapitalTransaction
{
    [Key]
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Group? Group { get; set; }
    public Guid FarmerId { get; set; }
    public AppUser? Farmer { get; set; }
    public DateTime ContributionDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LoanRepayment
{
    [Key]
    public Guid Id { get; set; }
    public Guid LoanId { get; set; }
    public Loan? Loan { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

public class AuditLog
{
    [Key]
    public Guid Id { get; set; }
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string Action { get; set; } = default!;
    public Guid PerformedById { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
}
