namespace AquaFarm.Core;

public enum UserRole
{
    Admin,
    Farmer,
    GroupManager
}

public enum TransactionType
{
    Investment,
    Expense,
    Revenue,
    GroupTransaction,
    LoanDisbursement,
    LoanRepayment
}

public enum InterestType
{
    Simple,
    Compound
}

public enum MembershipRole
{
    Member,
    Manager
}

public enum PayoutStatus
{
    Pending,
    Completed
}
