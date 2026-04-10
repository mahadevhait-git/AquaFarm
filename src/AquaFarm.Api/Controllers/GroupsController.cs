using AquaFarm.Core.Entities;
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
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly AquaFarmDbContext _dbContext;

    public GroupsController(AquaFarmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var groups = await _dbContext.Groups
            .Include(g => g.Manager)
            .Include(g => g.Members).ThenInclude(m => m.User)
            .Include(g => g.Ponds)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.ManagerId,
                ManagerName = g.Manager != null ? $"{g.Manager.FirstName} {g.Manager.LastName}" : null,
                FarmerCount = g.Members.Count(m => m.User != null && m.User.Role == UserRole.Farmer),
                PondCount = g.Ponds.Count(),
                g.CreatedAt
            })
            .ToListAsync();
        return Ok(groups);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GroupCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Group name is required.");
        }

        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        if (loggedInUser.Role != UserRole.GroupManager && loggedInUser.Role != UserRole.Admin)
        {
            return BadRequest("Only Group Manager or Admin can create groups.");
        }

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ManagerId = loggedInUser.Id,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { groupId = group.Id }, new
        {
            group.Id,
            group.Name,
            group.Description,
            group.ManagerId,
            group.CreatedAt
        });
    }

    [HttpPost("{groupId:guid}/farmers")]
    public async Task<IActionResult> AddFarmer(Guid groupId, [FromBody] AddFarmerToGroupRequest request)
    {
        var group = await _dbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null)
        {
            return NotFound("Group not found.");
        }

        var farmer = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.FarmerId);
        if (farmer is null)
        {
            return NotFound("Farmer not found.");
        }

        if (farmer.Role != UserRole.Farmer)
        {
            return BadRequest("Only users with Farmer role can be added to a group.");
        }

        var membershipExists = await _dbContext.GroupMemberships
            .AnyAsync(m => m.GroupId == groupId && m.UserId == request.FarmerId);

        if (membershipExists)
        {
            return BadRequest("Farmer is already a member of this group.");
        }

        _dbContext.GroupMemberships.Add(new GroupMembership
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = request.FarmerId,
            Role = MembershipRole.Member,
            JoinedAt = DateTime.UtcNow
        });

        var farmerPonds = await _dbContext.Ponds.Where(p => p.OwnerId == request.FarmerId).ToListAsync();
        foreach (var pond in farmerPonds)
        {
            pond.GroupId = groupId;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new
        {
            message = "Farmer added to group successfully.",
            groupId,
            farmerId = request.FarmerId,
            mappedPondCount = farmerPonds.Count
        });
    }

    [HttpGet("{groupId:guid}/farmers")]
    public async Task<IActionResult> GetFarmers(Guid groupId)
    {
        var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
        {
            return NotFound("Group not found.");
        }

        var farmers = await _dbContext.GroupMemberships
            .Where(m => m.GroupId == groupId && m.User != null && m.User.Role == UserRole.Farmer)
            .Select(m => new
            {
                m.UserId,
                Name = m.User != null ? $"{m.User.FirstName} {m.User.LastName}" : string.Empty,
                m.User!.PhoneNumber,
                m.User.Email,
                PondCount = _dbContext.Ponds.Count(p => p.OwnerId == m.UserId),
                m.JoinedAt
            })
            .ToListAsync();

        return Ok(farmers);
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

        var userName = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = User.FindFirstValue("sub");
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        return await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == userName);
    }
}
