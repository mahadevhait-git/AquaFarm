using AquaFarm.Core.Dtos;
using AquaFarm.Core.Entities;
using AquaFarm.Core;
using AquaFarm.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AquaFarm.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly AquaFarmDbContext _dbContext;

    public AdminUsersController(AquaFarmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("managers")]
    public async Task<IActionResult> GetManagers()
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var users = await GetUsersByRole(UserRole.GroupManager);
        return Ok(users);
    }

    [HttpGet("farmers")]
    public async Task<IActionResult> GetFarmers()
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var users = await GetUsersByRole(UserRole.Farmer);
        return Ok(users);
    }

    [HttpGet("{userId:guid}/ponds")]
    public async Task<IActionResult> GetAssociatedPonds(Guid userId)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        var groupIds = user.Role == UserRole.Farmer
            ? await _dbContext.GroupMemberships
                .Where(m => m.UserId == userId)
                .Select(m => m.GroupId)
                .ToListAsync()
            : new List<Guid>();

        var pondsQuery = _dbContext.Ponds
            .Include(p => p.Owner)
            .Include(p => p.Group)
            .AsQueryable();

        if (user.Role == UserRole.GroupManager)
        {
            pondsQuery = pondsQuery.Where(p =>
                p.OwnerId == userId
                || (p.Group != null && p.Group.ManagerId == userId));
        }
        else if (user.Role == UserRole.Farmer)
        {
            pondsQuery = pondsQuery.Where(p =>
                p.OwnerId == userId
                || (p.GroupId.HasValue && groupIds.Contains(p.GroupId.Value)));
        }
        else
        {
            pondsQuery = pondsQuery.Where(p => p.OwnerId == userId);
        }

        var ponds = await pondsQuery
            .OrderBy(p => p.Name)
            .Select(p => new UserAssociatedPondDto(
                p.Id,
                p.Name,
                p.Location,
                p.OwnerId,
                p.Owner != null ? $"{p.Owner.FirstName} {p.Owner.LastName}" : string.Empty,
                p.GroupId,
                p.Group != null ? p.Group.Name : null,
                user.Role == UserRole.GroupManager
                    ? (p.OwnerId == userId ? "Owned" : "Managed Group")
                    : user.Role == UserRole.Farmer
                        ? (p.OwnerId == userId ? "Owned" : "Group Member")
                        : "Owned",
                p.CreatedAt))
            .Distinct()
            .ToListAsync();

        return Ok(ponds);
    }

    private bool IsAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<UserDirectoryItemDto>> GetUsersByRole(UserRole role)
    {
        return await _dbContext.Users
            .Where(u => u.Role == role)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new UserDirectoryItemDto(
                u.Id,
                u.UserName,
                u.FirstName,
                u.LastName,
                u.Email,
                u.PhoneNumber,
                u.Role.ToString(),
                u.CreatedAt))
            .ToListAsync();
    }
}
