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
                MemberCount = g.Members.Count(),
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

    [HttpPost("{groupId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid groupId, [FromBody] AddMemberToGroupRequest request)
    {
        var group = await _dbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null)
        {
            return NotFound("Group not found.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        if (user.Role != UserRole.Farmer)
        {
            return BadRequest("Only users with Farmer role can be added to a group.");
        }

        var membershipExists = await _dbContext.GroupMemberships
            .AnyAsync(m => m.GroupId == groupId && m.UserId == request.UserId);

        if (membershipExists)
        {
            return BadRequest("User is already a member of this group.");
        }

        _dbContext.GroupMemberships.Add(new GroupMembership
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = request.UserId,
            Role = MembershipRole.Member,
            JoinedAt = DateTime.UtcNow
        });

        var userPonds = await _dbContext.Ponds.Where(p => p.OwnerId == request.UserId).ToListAsync();
        foreach (var pond in userPonds)
        {
            pond.GroupId = groupId;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new
        {
            message = "Member added to group successfully.",
            groupId,
            userId = request.UserId,
            mappedPondCount = userPonds.Count
        });
    }

    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid userId)
    {
        var membership = await _dbContext.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (membership is null)
        {
            return NotFound("Member not found in this group.");
        }

        _dbContext.GroupMemberships.Remove(membership);

        var userPonds = await _dbContext.Ponds
            .Where(p => p.OwnerId == userId && p.GroupId == groupId)
            .ToListAsync();
        foreach (var pond in userPonds)
        {
            pond.GroupId = null;
        }

        var loanTotals = await _dbContext.Loans
            .Where(l => l.GroupId == groupId && l.BorrowerId == userId)
            .ToListAsync();
        if (loanTotals.Count > 0)
        {
            _dbContext.Loans.RemoveRange(loanTotals);
        }

        var capitalTransactions = await _dbContext.CapitalTransactions
            .Where(c => c.GroupId == groupId && c.FarmerId == userId)
            .ToListAsync();
        if (capitalTransactions.Count > 0)
        {
            _dbContext.CapitalTransactions.RemoveRange(capitalTransactions);
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Member removed from group." });
    }

    [HttpGet("{groupId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid groupId, [FromQuery] string? search = null)
    {
        var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
        {
            return NotFound("Group not found.");
        }

        var normalizedSearch = search?.Trim().ToLowerInvariant();

        var membersQuery = _dbContext.GroupMemberships
            .Where(m => m.GroupId == groupId && m.User != null && m.User.Role == UserRole.Farmer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            membersQuery = membersQuery.Where(m =>
                (m.User!.FirstName + " " + m.User.LastName).ToLower().Contains(normalizedSearch) ||
                m.User!.PhoneNumber.ToLower().Contains(normalizedSearch) ||
                m.User!.Email.ToLower().Contains(normalizedSearch));
        }

        var members = await membersQuery.Select(m => new
            {
                m.UserId,
                Name = m.User != null ? $"{m.User.FirstName} {m.User.LastName}" : string.Empty,
                m.User!.PhoneNumber,
                m.User.Email,
                PondCount = _dbContext.Ponds.Count(p => p.OwnerId == m.UserId),
                m.JoinedAt
            })
            .ToListAsync();

        return Ok(members);
    }

    [HttpGet("{groupId:guid}/member-candidates")]
    public async Task<IActionResult> SearchMemberCandidates(Guid groupId, [FromQuery] string? search = null)
    {
        var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
        {
            return NotFound("Group not found.");
        }

        var normalizedSearch = search?.Trim().ToLowerInvariant();
        var existingMemberIds = await _dbContext.GroupMemberships
            .Where(m => m.GroupId == groupId)
            .Select(m => m.UserId)
            .ToListAsync();

        var candidatesQuery = _dbContext.Users
            .Where(u => u.Role == UserRole.Farmer && !existingMemberIds.Contains(u.Id))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            candidatesQuery = candidatesQuery.Where(u =>
                (u.FirstName + " " + u.LastName).ToLower().Contains(normalizedSearch) ||
                u.PhoneNumber.ToLower().Contains(normalizedSearch) ||
                u.Email.ToLower().Contains(normalizedSearch));
        }

        var candidates = await candidatesQuery
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new
            {
                userId = u.Id,
                name = u.FirstName + " " + u.LastName,
                u.PhoneNumber,
                u.Email
            })
            .Take(20)
            .ToListAsync();

        return Ok(candidates);
    }

    [HttpGet("{groupId:guid}/contributions")]
    public async Task<IActionResult> GetContributions(Guid groupId)
    {
        var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
        {
            return NotFound("Group not found.");
        }

        var members = await _dbContext.GroupMemberships
            .Where(m => m.GroupId == groupId && m.User != null && m.User.Role == UserRole.Farmer)
            .Select(m => new
            {
                m.UserId,
                Name = m.User!.FirstName + " " + m.User.LastName,
                m.User.PhoneNumber,
                m.User.Email
            })
            .ToListAsync();

        var contributionMap = await _dbContext.Loans
            .Where(l => l.GroupId == groupId)
            .GroupBy(l => l.BorrowerId)
            .Select(g => new { UserId = g.Key, Amount = g.Sum(x => x.PrincipalAmount) })
            .ToDictionaryAsync(x => x.UserId, x => x.Amount);

        var result = members.Select(m => new
        {
            m.UserId,
            m.Name,
            m.PhoneNumber,
            m.Email,
            InvestedAmount = contributionMap.TryGetValue(m.UserId, out var amount) ? amount : 0m
        });

        return Ok(result);
    }

    [HttpPut("{groupId:guid}/contributions")]
    public async Task<IActionResult> UpsertContribution(Guid groupId, [FromBody] UpsertGroupContributionRequest request)
    {
        if (request.Amount < 0)
        {
            return BadRequest("Contribution amount cannot be negative.");
        }

        var membership = await _dbContext.GroupMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == request.UserId);
        if (membership is null || membership.User is null)
        {
            return BadRequest("User is not a member of this group.");
        }

        if (membership.User.Role != UserRole.Farmer)
        {
            return BadRequest("Contribution can only be managed for farmers.");
        }

        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var existing = await _dbContext.Loans
            .FirstOrDefaultAsync(l => l.GroupId == groupId && l.BorrowerId == request.UserId);

        var oldAmount = existing?.PrincipalAmount ?? 0m;
        if (existing is null)
        {
            existing = new Loan
            {
                Id = Guid.NewGuid(),
                LenderId = loggedInUser.Id,
                BorrowerId = request.UserId,
                GroupId = groupId,
                PrincipalAmount = request.Amount,
                InterestRate = 0m,
                InterestType = InterestType.Simple,
                TermMonths = 0,
                OutstandingBalance = request.Amount,
                StartDate = DateTime.UtcNow,
                IsClosed = request.Amount <= 0m,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Loans.Add(existing);
        }
        else
        {
            existing.LenderId = loggedInUser.Id;
            existing.PrincipalAmount = request.Amount;
            existing.OutstandingBalance = request.Amount;
            existing.StartDate = DateTime.UtcNow;
            existing.IsClosed = request.Amount <= 0m;
        }

        var delta = request.Amount - oldAmount;
        if (delta != 0m)
        {
            _dbContext.CapitalTransactions.Add(new CapitalTransaction
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                FarmerId = request.UserId,
                ContributionDate = DateTime.UtcNow,
                Amount = delta,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Contribution saved successfully." });
    }

    [HttpPost("{groupId:guid}/contributions/record")]
    public async Task<IActionResult> RecordContribution(Guid groupId, [FromBody] RecordGroupContributionRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest("Contribution amount must be greater than zero.");
        }

        var membership = await _dbContext.GroupMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == request.UserId);
        if (membership is null || membership.User is null)
        {
            return BadRequest("User is not a member of this group.");
        }

        if (membership.User.Role != UserRole.Farmer)
        {
            return BadRequest("Contribution can only be recorded for farmers.");
        }

        _dbContext.CapitalTransactions.Add(new CapitalTransaction
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            FarmerId = request.UserId,
            ContributionDate = DateTime.UtcNow,
            Amount = request.Amount,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        await RecalculateLoanTotalFromTransactions(groupId, request.UserId);
        return Ok(new { message = "Contribution recorded successfully." });
    }

    [HttpDelete("{groupId:guid}/contributions/{userId:guid}")]
    public async Task<IActionResult> DeleteContribution(Guid groupId, Guid userId)
    {
        var existing = await _dbContext.Loans
            .Where(l => l.GroupId == groupId && l.BorrowerId == userId)
            .ToListAsync();

        if (existing.Count == 0)
        {
            return NotFound("No contribution record found for this member.");
        }

        _dbContext.Loans.RemoveRange(existing);

        var contributionTransactions = await _dbContext.CapitalTransactions
            .Where(c => c.GroupId == groupId && c.FarmerId == userId)
            .ToListAsync();
        if (contributionTransactions.Count > 0)
        {
            _dbContext.CapitalTransactions.RemoveRange(contributionTransactions);
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Contribution removed successfully." });
    }

    [HttpGet("{groupId:guid}/capital-transactions")]
    public async Task<IActionResult> GetCapitalTransactions(Guid groupId, [FromQuery] Guid? farmerId = null)
    {
        var groupExists = await _dbContext.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
        {
            return NotFound("Group not found.");
        }

        var query = _dbContext.CapitalTransactions
            .Where(c => c.GroupId == groupId)
            .Include(c => c.Farmer)
            .AsQueryable();

        if (farmerId.HasValue && farmerId.Value != Guid.Empty)
        {
            query = query.Where(c => c.FarmerId == farmerId.Value);
        }

        var result = await query
            .OrderByDescending(c => c.ContributionDate)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.GroupId,
                c.FarmerId,
                FarmerName = c.Farmer != null ? c.Farmer.FirstName + " " + c.Farmer.LastName : string.Empty,
                c.ContributionDate,
                c.Amount,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpPut("{groupId:guid}/capital-transactions/{transactionId:guid}")]
    public async Task<IActionResult> UpdateCapitalTransactionAmount(
        Guid groupId,
        Guid transactionId,
        [FromBody] UpdateCapitalTransactionAmountRequest request)
    {
        if (request.Amount < 0)
        {
            return BadRequest("Contribution amount cannot be negative.");
        }

        var transaction = await _dbContext.CapitalTransactions
            .FirstOrDefaultAsync(c => c.Id == transactionId && c.GroupId == groupId);
        if (transaction is null)
        {
            return NotFound("Capital transaction not found.");
        }

        transaction.Amount = request.Amount;
        await _dbContext.SaveChangesAsync();

        await RecalculateLoanTotalFromTransactions(groupId, transaction.FarmerId);
        return Ok(new { message = "Capital transaction updated successfully." });
    }

    private async Task RecalculateLoanTotalFromTransactions(Guid groupId, Guid farmerId)
    {
        var total = await _dbContext.CapitalTransactions
            .Where(c => c.GroupId == groupId && c.FarmerId == farmerId)
            .SumAsync(c => c.Amount);

        var loggedInUser = await GetLoggedInUser();
        var defaultLenderId = loggedInUser?.Id
            ?? await _dbContext.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).FirstOrDefaultAsync();
        if (defaultLenderId == Guid.Empty)
        {
            defaultLenderId = await _dbContext.Users.Select(u => u.Id).FirstOrDefaultAsync();
        }

        var loan = await _dbContext.Loans
            .FirstOrDefaultAsync(l => l.GroupId == groupId && l.BorrowerId == farmerId);

        if (loan is null && total <= 0)
        {
            return;
        }

        if (loan is null)
        {
            loan = new Loan
            {
                Id = Guid.NewGuid(),
                LenderId = defaultLenderId,
                BorrowerId = farmerId,
                GroupId = groupId,
                PrincipalAmount = total,
                InterestRate = 0m,
                InterestType = InterestType.Simple,
                TermMonths = 0,
                OutstandingBalance = total,
                StartDate = DateTime.UtcNow,
                IsClosed = total <= 0m,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Loans.Add(loan);
        }
        else
        {
            loan.LenderId = defaultLenderId;
            loan.PrincipalAmount = total;
            loan.OutstandingBalance = total;
            loan.StartDate = DateTime.UtcNow;
            loan.IsClosed = total <= 0m;
        }

        await _dbContext.SaveChangesAsync();
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
