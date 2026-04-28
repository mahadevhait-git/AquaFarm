using AquaFarm.Core;
using AquaFarm.Core.Dtos;
using AquaFarm.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AquaFarm.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/audit")]
public class AdminAuditController : ControllerBase
{
    private readonly AquaFarmDbContext _dbContext;

    public AdminAuditController(AquaFarmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("investments-expenses")]
    public async Task<IActionResult> GetInvestmentExpenseAudits()
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var rows = await _dbContext.InvestmentExpenseAudits
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new InvestmentExpenseAuditDto(
                a.Id,
                a.RecordType,
                a.ActionType,
                a.RecordId,
                a.GroupId,
                a.PondId,
                a.FarmerId,
                a.OldAmount,
                a.NewAmount,
                a.OldValuesJson,
                a.NewValuesJson,
                a.PerformedById,
                a.PerformedByUserName,
                a.CreatedAt))
            .ToListAsync();

        return Ok(rows);
    }

    private bool IsAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
