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
public class PondsController : ControllerBase
{
    private readonly AquaFarmDbContext _dbContext;

    public PondsController(AquaFarmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var farmerGroupIds = loggedInUser.Role == UserRole.Farmer
            ? await _dbContext.GroupMemberships
                .Where(m => m.UserId == loggedInUser.Id)
                .Select(m => m.GroupId)
                .ToListAsync()
            : new List<Guid>();

        var ponds = await _dbContext.Ponds
            .Include(p => p.Owner)
            .Where(p =>
                loggedInUser.Role == UserRole.Admin
                || p.OwnerId == loggedInUser.Id
                || (loggedInUser.Role == UserRole.Farmer && p.GroupId.HasValue && farmerGroupIds.Contains(p.GroupId.Value)))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Location,
                p.OwnerId,
                OwnerName = p.Owner != null ? (p.Owner.FirstName + " " + p.Owner.LastName) : string.Empty,
                p.GroupId,
                p.CreatedAt
            })
            .ToListAsync();
        return Ok(ponds);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var farmerGroupIds = loggedInUser.Role == UserRole.Farmer
            ? await _dbContext.GroupMemberships
                .Where(m => m.UserId == loggedInUser.Id)
                .Select(m => m.GroupId)
                .ToListAsync()
            : new List<Guid>();

        var pond = await _dbContext.Ponds
            .Include(p => p.Owner)
            .Where(p =>
                p.Id == id
                && (loggedInUser.Role == UserRole.Admin
                    || p.OwnerId == loggedInUser.Id
                    || (loggedInUser.Role == UserRole.Farmer && p.GroupId.HasValue && farmerGroupIds.Contains(p.GroupId.Value))))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Location,
                p.OwnerId,
                OwnerName = p.Owner != null ? (p.Owner.FirstName + " " + p.Owner.LastName) : string.Empty,
                p.GroupId,
                p.CreatedAt
            })
            .FirstOrDefaultAsync();
        if (pond is null)
        {
            return NotFound();
        }

        return Ok(pond);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PondCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Pond name is required.");
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

        if (request.GroupId.HasValue)
        {
            var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == request.GroupId.Value);
            if (!groupExists)
            {
                return BadRequest("GroupId does not reference an existing group.");
            }
        }

        var pond = new Pond
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Location = request.Location?.Trim(),
            OwnerId = loggedInUser.Id,
            GroupId = request.GroupId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Ponds.Add(pond);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = pond.Id }, new
        {
            pond.Id,
            pond.Name,
            pond.Location,
            pond.OwnerId,
            OwnerName = loggedInUser.FirstName + " " + loggedInUser.LastName,
            pond.GroupId,
            pond.CreatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PondUpdateRequest update)
    {
        var existing = await _dbContext.Ponds.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
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

        if (loggedInUser.Role != UserRole.Admin && existing.OwnerId != loggedInUser.Id)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(update.Name))
        {
            return BadRequest("Pond name is required.");
        }

        if (update.GroupId.HasValue)
        {
            var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == update.GroupId.Value);
            if (!groupExists)
            {
                return BadRequest("GroupId does not reference an existing group.");
            }
        }

        existing.Name = update.Name.Trim();
        existing.Location = update.Location?.Trim();
        existing.GroupId = update.GroupId;
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _dbContext.Ponds.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
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

        if (loggedInUser.Role != UserRole.Admin && existing.OwnerId != loggedInUser.Id)
        {
            return Forbid();
        }

        _dbContext.Ponds.Remove(existing);
        await _dbContext.SaveChangesAsync();
        return NoContent();
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

        var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        return await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == userName);
    }
}
