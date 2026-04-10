namespace AquaFarm.Core.Dtos;

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Address,
    string Email,
    string PhoneNumber,
    string Password,
    string Role = "Farmer");
public record LoginRequest(string PhoneNumber, string Password);
public record ForgotPasswordOtpRequest(string Email);
public record ForgotPasswordResetRequest(string Email, string Otp, string NewPassword);
public record AuthResponse(string Token, string Role);

public record PondCreateRequest(string Name, string? Location, Guid OwnerId, Guid? GroupId);
public record PondDto(Guid Id, string Name, string? Location, Guid OwnerId, Guid? GroupId, DateTime CreatedAt);

public record TransactionCreateRequest(TransactionType Type, string Category, decimal Amount, DateTime Date, Guid CreatedById, Guid? PondId = null, Guid? GroupId = null, string? Notes = null);
public record TransactionDto(Guid Id, TransactionType Type, string Category, decimal Amount, DateTime Date, Guid? PondId, Guid? GroupId, Guid CreatedById, string? Notes);

public record GroupCreateRequest(string Name, string? Description);
public record GroupDto(Guid Id, string Name, string? Description, DateTime CreatedAt);
public record AddFarmerToGroupRequest(Guid FarmerId);

public record LoanCreateRequest(Guid BorrowerId, decimal PrincipalAmount, decimal InterestRate, InterestType InterestType, int TermMonths, string? Notes = null);
public record LoanRepaymentRequest(decimal Amount, DateTime Date, string? Notes = null);
public record LoanDto(Guid Id, Guid LenderId, Guid BorrowerId, decimal PrincipalAmount, decimal InterestRate, InterestType InterestType, int TermMonths, decimal OutstandingBalance, bool IsClosed, DateTime StartDate, DateTime CreatedAt);
public record LoanSummaryDto(Guid Id, decimal PrincipalAmount, decimal OutstandingBalance, decimal AccruedInterest, InterestType InterestType, DateTime StartDate, int TermMonths, bool IsClosed);
