using AquaFarm.Core.Dtos;
using AquaFarm.Core.Entities;
using AquaFarm.Core.Interfaces;
using AquaFarm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AquaFarm.Infrastructure.Services;

public class LoanService : ILoanService
{
    private readonly AquaFarmDbContext _dbContext;
    private readonly IFinancialCalculator _calculator;

    public LoanService(AquaFarmDbContext dbContext, IFinancialCalculator calculator)
    {
        _dbContext = dbContext;
        _calculator = calculator;
    }

    public Loan CreateLoan(LoanCreateRequest request, Guid lenderId)
    {
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LenderId = lenderId,
            BorrowerId = request.BorrowerId,
            PrincipalAmount = request.PrincipalAmount,
            InterestRate = request.InterestRate,
            InterestType = request.InterestType,
            TermMonths = request.TermMonths,
            StartDate = DateTime.UtcNow,
            OutstandingBalance = request.PrincipalAmount,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Loans.Add(loan);
        _dbContext.SaveChanges();
        return loan;
    }

    public LoanSummaryDto GetLoanSummary(Guid loanId)
    {
        var loan = _dbContext.Loans.Include(l => l.Repayments).FirstOrDefault(l => l.Id == loanId)
                   ?? throw new InvalidOperationException("Loan not found.");

        var accruedInterest = _calculator.CalculateLoanInterest(loan.PrincipalAmount, loan.InterestRate, loan.InterestType, loan.StartDate, DateTime.UtcNow);
        var totalRepayments = loan.Repayments.Sum(r => r.Amount);
        var outstanding = Math.Max(0, loan.PrincipalAmount + accruedInterest - totalRepayments);

        return new LoanSummaryDto(
            loan.Id,
            loan.PrincipalAmount,
            outstanding,
            accruedInterest,
            loan.InterestType,
            loan.StartDate,
            loan.TermMonths,
            loan.IsClosed);
    }

    public LoanRepayment RegisterRepayment(Guid loanId, LoanRepaymentRequest request)
    {
        var loan = _dbContext.Loans.Include(l => l.Repayments).FirstOrDefault(l => l.Id == loanId)
                   ?? throw new InvalidOperationException("Loan not found.");

        var repayment = new LoanRepayment
        {
            Id = Guid.NewGuid(),
            LoanId = loanId,
            Amount = request.Amount,
            Date = request.Date,
            Notes = request.Notes
        };

        loan.Repayments.Add(repayment);
        var accruedInterest = _calculator.CalculateLoanInterest(loan.PrincipalAmount, loan.InterestRate, loan.InterestType, loan.StartDate, DateTime.UtcNow);
        var totalRepayments = loan.Repayments.Sum(r => r.Amount);
        loan.OutstandingBalance = Math.Max(0, loan.PrincipalAmount + accruedInterest - totalRepayments);
        loan.IsClosed = loan.OutstandingBalance <= 0;

        _dbContext.LoanRepayments.Add(repayment);
        _dbContext.SaveChanges();

        return repayment;
    }
}
