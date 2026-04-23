using AquaFarm.Core.Dtos;
using AquaFarm.Core.Interfaces;
using AquaFarm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AquaFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoansController : ControllerBase
{
    private readonly ILoanService _loanService;
    private readonly AquaFarmDbContext _dbContext;

    public LoansController(ILoanService loanService, AquaFarmDbContext dbContext)
    {
        _loanService = loanService;
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> CreateLoan([FromBody] LoanCreateRequest request)
    {
        var borrowerExists = await _dbContext.Users.AnyAsync(u => u.Id == request.BorrowerId);
        if (!borrowerExists)
        {
            return BadRequest("BorrowerId does not reference an existing user.");
        }

        var userName = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.Name);
        Guid lenderId;

        if (!string.IsNullOrWhiteSpace(userName))
        {
            lenderId = await _dbContext.Users
                .Where(u => u.UserName == userName)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();
        }
        else
        {
            lenderId = Guid.Empty;
        }

        if (lenderId == Guid.Empty)
        {
            lenderId = await _dbContext.Users.Select(u => u.Id).FirstOrDefaultAsync();
        }

        if (lenderId == Guid.Empty)
        {
            return BadRequest("No valid lender user found.");
        }

        var loan = _loanService.CreateLoan(request, lenderId);
        return CreatedAtAction(nameof(GetSummary), new { loanId = loan.Id }, loan);
    }

    [HttpPost("{loanId}/repay")]
    public IActionResult RepayLoan(Guid loanId, [FromBody] LoanRepaymentRequest request)
    {
        var repayment = _loanService.RegisterRepayment(loanId, request);
        return Ok(repayment);
    }

    [HttpGet("{loanId}/summary")]
    public IActionResult GetSummary(Guid loanId)
    {
        var summary = _loanService.GetLoanSummary(loanId);
        return Ok(summary);
    }
}
