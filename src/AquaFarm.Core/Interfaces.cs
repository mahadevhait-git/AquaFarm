using AquaFarm.Core.Dtos;
using AquaFarm.Core.Entities;

namespace AquaFarm.Core.Interfaces;

public interface IFinancialCalculator
{
    decimal CalculateProfitLoss(IEnumerable<Transaction> transactions);
    decimal CalculateLoanInterest(decimal principal, decimal annualRate, InterestType interestType, DateTime startDate, DateTime asOf);
}

public interface ILoanService
{
    Loan CreateLoan(LoanCreateRequest request, Guid lenderId);
    LoanSummaryDto GetLoanSummary(Guid loanId);
    LoanRepayment RegisterRepayment(Guid loanId, LoanRepaymentRequest request);
}
