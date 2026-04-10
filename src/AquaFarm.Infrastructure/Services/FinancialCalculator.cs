using AquaFarm.Core;
using AquaFarm.Core.Entities;
using AquaFarm.Core.Interfaces;

namespace AquaFarm.Infrastructure.Services;

public class FinancialCalculator : IFinancialCalculator
{
    public decimal CalculateProfitLoss(IEnumerable<Transaction> transactions)
    {
        var revenue = transactions.Where(t => t.Type == TransactionType.Revenue).Sum(t => t.Amount);
        var investment = transactions.Where(t => t.Type == TransactionType.Investment).Sum(t => t.Amount);
        var expenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        var loanRepayments = transactions.Where(t => t.Type == TransactionType.LoanRepayment).Sum(t => t.Amount);

        return revenue - (investment + expenses + loanRepayments);
    }

    public decimal CalculateLoanInterest(decimal principal, decimal annualRate, InterestType interestType, DateTime startDate, DateTime asOf)
    {
        if (principal <= 0 || annualRate <= 0 || asOf <= startDate)
        {
            return 0m;
        }

        var totalDays = (asOf - startDate).TotalDays;
        var years = (decimal)totalDays / 365m;
        var rate = annualRate / 100m;

        return interestType switch
        {
            InterestType.Simple => principal * rate * years,
            InterestType.Compound => principal * (decimal)Math.Pow((double)(1 + rate), (double)years) - principal,
            _ => 0m,
        };
    }
}
