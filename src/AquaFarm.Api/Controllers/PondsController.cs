using AquaFarm.Core.Entities;
using AquaFarm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaFarm.Api.Controllers;

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
        var ponds = await _dbContext.Ponds
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Location,
                p.OwnerId,
                p.GroupId,
                p.CreatedAt
            })
            .ToListAsync();
        return Ok(ponds);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var pond = await _dbContext.Ponds
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Location,
                p.OwnerId,
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
    public async Task<IActionResult> Create([FromBody] Pond pond)
    {
        var ownerExists = await _dbContext.Users.AnyAsync(u => u.Id == pond.OwnerId);
        if (!ownerExists)
        {
            return BadRequest("OwnerId does not reference an existing user.");
        }

        if (pond.GroupId.HasValue)
        {
            var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == pond.GroupId.Value);
            if (!groupExists)
            {
                return BadRequest("GroupId does not reference an existing group.");
            }
        }

        pond.Id = Guid.NewGuid();
        pond.CreatedAt = DateTime.UtcNow;
        _dbContext.Ponds.Add(pond);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = pond.Id }, new
        {
            pond.Id,
            pond.Name,
            pond.Location,
            pond.OwnerId,
            pond.GroupId,
            pond.CreatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Pond update)
    {
        var existing = await _dbContext.Ponds.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        var ownerExists = await _dbContext.Users.AnyAsync(u => u.Id == update.OwnerId);
        if (!ownerExists)
        {
            return BadRequest("OwnerId does not reference an existing user.");
        }

        if (update.GroupId.HasValue)
        {
            var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == update.GroupId.Value);
            if (!groupExists)
            {
                return BadRequest("GroupId does not reference an existing group.");
            }
        }

        existing.Name = update.Name;
        existing.Location = update.Location;
        existing.GroupId = update.GroupId;
        existing.OwnerId = update.OwnerId;
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

        _dbContext.Ponds.Remove(existing);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }
}
