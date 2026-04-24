using AquaFarm.Core.Dtos;
using AquaFarm.Core.Entities;
using AquaFarm.Core;
using AquaFarm.Core.Interfaces;
using AquaFarm.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AquaFarm.Api.Controllers;

[Authorize]
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

        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }
        if (loggedInUser.Role == UserRole.Farmer)
        {
            return Forbid();
        }

        var loan = _loanService.CreateLoan(request, loggedInUser.Id);
        return CreatedAtAction(nameof(GetSummary), new { loanId = loan.Id }, loan);
    }

    [HttpPost("{loanId}/repay")]
    public async Task<IActionResult> RepayLoan(Guid loanId, [FromBody] LoanRepaymentRequest request)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }
        if (loggedInUser.Role == UserRole.Farmer)
        {
            return Forbid();
        }

        var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == loanId);
        if (loan is null)
        {
            return NotFound("Loan not found.");
        }

        if (loggedInUser.Role == UserRole.GroupManager && loan.LenderId != loggedInUser.Id)
        {
            return Forbid();
        }

        var repayment = _loanService.RegisterRepayment(loanId, request);
        return Ok(repayment);
    }

    [HttpGet("{loanId}/summary")]
    public async Task<IActionResult> GetSummary(Guid loanId)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == loanId);
        if (loan is null)
        {
            return NotFound("Loan not found.");
        }

        if (loggedInUser.Role == UserRole.GroupManager && loan.LenderId != loggedInUser.Id)
        {
            return Forbid();
        }

        var summary = _loanService.GetLoanSummary(loanId);
        return Ok(summary);
    }

    private async Task<AppUser?> GetLoggedInUser()
    {
        var nameIdentifier = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(nameIdentifier) && Guid.TryParse(nameIdentifier, out var userId))
        {
            var byId = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (byId is not null)
            {
                return byId;
            }
        }

        var userName = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        return await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == userName);
    }
}
