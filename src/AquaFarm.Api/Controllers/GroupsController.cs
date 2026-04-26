using AquaFarm.Core.Entities;
using AquaFarm.Core;
using AquaFarm.Core.Dtos;
using AquaFarm.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Mail;

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
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var groups = await _dbContext.Groups
            .Include(g => g.Manager)
            .Include(g => g.Members).ThenInclude(m => m.User)
            .Include(g => g.Ponds)
            .Where(g =>
                (loggedInUser.Role == UserRole.GroupManager && g.ManagerId == loggedInUser.Id)
                || (loggedInUser.Role == UserRole.Farmer && g.Members.Any(m => m.UserId == loggedInUser.Id))
                || loggedInUser.Role == UserRole.Admin)
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
        if (loggedInUser.Role == UserRole.Farmer)
        {
            return Forbid();
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
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
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

    [HttpPost("{groupId:guid}/farmers")]
    public async Task<IActionResult> CreateFarmer(Guid groupId, [FromBody] CreateFarmerInGroupRequest request)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
        {
            return NotFound("Group not found.");
        }

        if (string.IsNullOrWhiteSpace(request.FirstName)
            || string.IsNullOrWhiteSpace(request.LastName)
            || string.IsNullOrWhiteSpace(request.Address)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest("First name, last name, address, email, and phone number are required.");
        }

        var normalizedPhone = NormalizePhone(request.PhoneNumber);
        if (normalizedPhone.Length < 10 || normalizedPhone.Length > 15)
        {
            return BadRequest("Phone number must contain 10 to 15 digits.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (!IsValidEmail(normalizedEmail))
        {
            return BadRequest("Please enter a valid email address.");
        }

        var phoneExists = await _dbContext.Users.AnyAsync(u => u.PhoneNumber == normalizedPhone);
        if (phoneExists)
        {
            return BadRequest("Phone number is already registered.");
        }

        var emailExists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (emailExists)
        {
            return BadRequest("Email is already registered.");
        }

        var generatedPassword = GenerateTemporaryPassword();
        var userName = await GenerateUserName(request.FirstName, request.LastName);

        var farmer = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Address = request.Address.Trim(),
            Email = normalizedEmail,
            PhoneNumber = normalizedPhone,
            PasswordHash = generatedPassword,
            Role = UserRole.Farmer,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(farmer);

        _dbContext.GroupMemberships.Add(new GroupMembership
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = farmer.Id,
            Role = MembershipRole.Member,
            JoinedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Farmer created and added to group successfully.",
            userId = farmer.Id,
            name = farmer.FirstName + " " + farmer.LastName,
            userName = farmer.UserName,
            email = farmer.Email,
            phoneNumber = farmer.PhoneNumber,
            autoGeneratedPassword = generatedPassword
        });
    }

    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid userId)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
        {
            return NotFound("Group not found.");
        }

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
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
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
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
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
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }
        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
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

        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }
        if (loggedInUser.Role == UserRole.Farmer)
        {
            return Forbid();
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
        {
            return NotFound("Group not found.");
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

        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }
        if (loggedInUser.Role == UserRole.Farmer)
        {
            return Forbid();
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
        {
            return NotFound("Group not found.");
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
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }
        if (loggedInUser.Role == UserRole.Farmer)
        {
            return Forbid();
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
        {
            return NotFound("Group not found.");
        }

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
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
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

        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }
        if (loggedInUser.Role == UserRole.Farmer)
        {
            return Forbid();
        }

        var group = await GetAccessibleGroup(groupId, loggedInUser);
        if (group is null)
        {
            return NotFound("Group not found.");
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

    [HttpGet("payouts/setup")]
    public async Task<IActionResult> GetPayoutSetup(
        [FromQuery] Guid pondId,
        [FromQuery] Guid farmerId,
        [FromQuery] decimal annualInterestRate = 0m)
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

        if (pondId == Guid.Empty || farmerId == Guid.Empty)
        {
            return BadRequest("Pond and farmer are required.");
        }

        var pond = await _dbContext.Ponds
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.Id == pondId);
        if (pond is null)
        {
            return NotFound("Pond not found.");
        }

        if (!CanManagePond(loggedInUser, pond))
        {
            return Forbid();
        }

        if (!pond.GroupId.HasValue)
        {
            return BadRequest("Selected pond is not linked to any group.");
        }

        var membership = await _dbContext.GroupMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.GroupId == pond.GroupId.Value && m.UserId == farmerId);
        if (membership is null || membership.User is null || membership.User.Role != UserRole.Farmer)
        {
            return BadRequest("Selected farmer is not a member of the pond group.");
        }

        var payouts = await _dbContext.ContributionPayouts
            .Where(p => p.PondId == pondId && p.FarmerId == farmerId)
            .ToDictionaryAsync(p => p.CapitalTransactionId, p => p);

        var rows = await _dbContext.CapitalTransactions
            .Where(c => c.GroupId == pond.GroupId.Value && c.FarmerId == farmerId && c.Amount > 0)
            .OrderBy(c => c.ContributionDate)
            .ThenBy(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.ContributionDate,
                PrincipalAmount = c.Amount
            })
            .ToListAsync();

        var normalizedRate = annualInterestRate < 0 ? 0m : annualInterestRate;
        var result = rows.Select(row =>
        {
            var existingPayout = payouts.TryGetValue(row.Id, out var payout) ? payout : null;
            var interest = CalculateSimpleInterest(row.PrincipalAmount, row.ContributionDate, normalizedRate);
            return new
            {
                capitalTransactionId = row.Id,
                contributionDate = row.ContributionDate,
                principalAmount = row.PrincipalAmount,
                annualInterestRate = normalizedRate,
                interestAmount = interest,
                totalAmount = row.PrincipalAmount + interest,
                canSelect = existingPayout is null || existingPayout.Status == PayoutStatus.Rejected,
                status = existingPayout?.Status.ToString(),
                paidAt = existingPayout?.PaidAt,
                confirmedAt = existingPayout?.ConfirmedAt
            };
        });

        return Ok(new
        {
            pondId = pond.Id,
            pondName = pond.Name,
            farmerId = membership.UserId,
            farmerName = $"{membership.User.FirstName} {membership.User.LastName}",
            annualInterestRate = normalizedRate,
            rows = result
        });
    }

    [HttpPost("payouts")]
    public async Task<IActionResult> CreatePayouts([FromBody] CreateContributionPayoutRequest request)
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

        if (request.PondId == Guid.Empty || request.FarmerId == Guid.Empty)
        {
            return BadRequest("Pond and farmer are required.");
        }

        if (request.CapitalTransactionIds is null || request.CapitalTransactionIds.Count == 0)
        {
            return BadRequest("Please select at least one contribution entry.");
        }

        var pond = await _dbContext.Ponds
            .FirstOrDefaultAsync(p => p.Id == request.PondId);
        if (pond is null)
        {
            return NotFound("Pond not found.");
        }

        if (!CanManagePond(loggedInUser, pond))
        {
            return Forbid();
        }

        if (!pond.GroupId.HasValue)
        {
            return BadRequest("Selected pond is not linked to any group.");
        }

        var membership = await _dbContext.GroupMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.GroupId == pond.GroupId.Value && m.UserId == request.FarmerId);
        if (membership is null || membership.User is null || membership.User.Role != UserRole.Farmer)
        {
            return BadRequest("Selected farmer is not a member of this pond group.");
        }

        var transactionIds = request.CapitalTransactionIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (transactionIds.Count == 0)
        {
            return BadRequest("Please select valid contribution entries.");
        }

        var transactions = await _dbContext.CapitalTransactions
            .Where(c => transactionIds.Contains(c.Id)
                && c.GroupId == pond.GroupId.Value
                && c.FarmerId == request.FarmerId
                && c.Amount > 0)
            .ToListAsync();
        if (transactions.Count == 0)
        {
            return BadRequest("No valid contribution entries were selected.");
        }

        var existingPayouts = await _dbContext.ContributionPayouts
            .Where(p => p.PondId == request.PondId && p.FarmerId == request.FarmerId && transactionIds.Contains(p.CapitalTransactionId))
            .ToDictionaryAsync(p => p.CapitalTransactionId, p => p);

        var payableTransactions = transactions
            .Where(c => !existingPayouts.TryGetValue(c.Id, out var payout) || payout.Status == PayoutStatus.Rejected)
            .ToList();
        if (payableTransactions.Count == 0)
        {
            return BadRequest("Selected entries are already marked for payment.");
        }

        var normalizedRate = request.AnnualInterestRate < 0 ? 0m : request.AnnualInterestRate;
        var created = new List<object>();

        foreach (var transaction in payableTransactions)
        {
            var interest = CalculateSimpleInterest(transaction.Amount, transaction.ContributionDate, normalizedRate);

            if (existingPayouts.TryGetValue(transaction.Id, out var existingPayout) && existingPayout.Status == PayoutStatus.Rejected)
            {
                existingPayout.ManagerId = loggedInUser.Id;
                existingPayout.ContributionDate = transaction.ContributionDate;
                existingPayout.PrincipalAmount = transaction.Amount;
                existingPayout.AnnualInterestRate = normalizedRate;
                existingPayout.InterestAmount = interest;
                existingPayout.TotalAmount = transaction.Amount + interest;
                existingPayout.Status = PayoutStatus.Pending;
                existingPayout.PaidAt = DateTime.UtcNow;
                existingPayout.ConfirmedAt = null;

                created.Add(new
                {
                    payoutId = existingPayout.Id,
                    capitalTransactionId = existingPayout.CapitalTransactionId,
                    existingPayout.PrincipalAmount,
                    existingPayout.InterestAmount,
                    existingPayout.TotalAmount,
                    status = existingPayout.Status.ToString()
                });
                continue;
            }

            var payout = new ContributionPayout
            {
                Id = Guid.NewGuid(),
                PondId = request.PondId,
                GroupId = pond.GroupId.Value,
                FarmerId = request.FarmerId,
                ManagerId = loggedInUser.Id,
                CapitalTransactionId = transaction.Id,
                ContributionDate = transaction.ContributionDate,
                PrincipalAmount = transaction.Amount,
                AnnualInterestRate = normalizedRate,
                InterestAmount = interest,
                TotalAmount = transaction.Amount + interest,
                Status = PayoutStatus.Pending,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ContributionPayouts.Add(payout);
            created.Add(new
            {
                payoutId = payout.Id,
                capitalTransactionId = payout.CapitalTransactionId,
                payout.PrincipalAmount,
                payout.InterestAmount,
                payout.TotalAmount,
                status = payout.Status.ToString()
            });
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Payout entries submitted successfully.",
            pondId = pond.Id,
            pondName = pond.Name,
            farmerId = request.FarmerId,
            farmerName = $"{membership.User.FirstName} {membership.User.LastName}",
            count = created.Count,
            payouts = created
        });
    }

    [HttpGet("payouts/farmer")]
    public async Task<IActionResult> GetFarmerPayouts([FromQuery] Guid? pondId = null)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        IQueryable<ContributionPayout> query = _dbContext.ContributionPayouts
            .Include(p => p.Pond)
            .Include(p => p.Manager)
            .AsQueryable();

        if (loggedInUser.Role == UserRole.Farmer)
        {
            query = query.Where(p => p.FarmerId == loggedInUser.Id);
        }
        else if (loggedInUser.Role == UserRole.GroupManager)
        {
            query = query.Where(p => p.ManagerId == loggedInUser.Id);
        }

        if (pondId.HasValue && pondId.Value != Guid.Empty)
        {
            query = query.Where(p => p.PondId == pondId.Value);
        }

        var result = await query
            .OrderByDescending(p => p.PaidAt)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                payoutId = p.Id,
                p.PondId,
                pondName = p.Pond != null ? p.Pond.Name : string.Empty,
                p.FarmerId,
                p.ManagerId,
                managerName = p.Manager != null ? $"{p.Manager.FirstName} {p.Manager.LastName}" : string.Empty,
                p.CapitalTransactionId,
                p.ContributionDate,
                p.PrincipalAmount,
                p.AnnualInterestRate,
                p.InterestAmount,
                p.TotalAmount,
                status = p.Status.ToString(),
                p.PaidAt,
                p.ConfirmedAt
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpPost("payouts/{payoutId:guid}/confirm")]
    public async Task<IActionResult> ConfirmFarmerPayout(Guid payoutId)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }
        if (loggedInUser.Role != UserRole.Farmer)
        {
            return Forbid();
        }

        var payout = await _dbContext.ContributionPayouts.FirstOrDefaultAsync(p => p.Id == payoutId);
        if (payout is null)
        {
            return NotFound("Payout entry not found.");
        }

        if (payout.FarmerId != loggedInUser.Id)
        {
            return Forbid();
        }

        if (payout.Status == PayoutStatus.Completed)
        {
            return Ok(new { message = "Payment already confirmed.", status = payout.Status.ToString() });
        }

        if (payout.Status == PayoutStatus.Rejected)
        {
            return BadRequest("Rejected payment cannot be confirmed.");
        }

        payout.Status = PayoutStatus.Completed;
        payout.ConfirmedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Payment confirmed successfully.",
            payoutId = payout.Id,
            status = payout.Status.ToString(),
            payout.ConfirmedAt
        });
    }

    [HttpPost("payouts/{payoutId:guid}/reject")]
    public async Task<IActionResult> RejectFarmerPayout(Guid payoutId)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }
        if (loggedInUser.Role != UserRole.Farmer)
        {
            return Forbid();
        }

        var payout = await _dbContext.ContributionPayouts.FirstOrDefaultAsync(p => p.Id == payoutId);
        if (payout is null)
        {
            return NotFound("Payout entry not found.");
        }

        if (payout.FarmerId != loggedInUser.Id)
        {
            return Forbid();
        }

        if (payout.Status == PayoutStatus.Completed)
        {
            return BadRequest("Completed payment cannot be rejected.");
        }

        if (payout.Status == PayoutStatus.Rejected)
        {
            return Ok(new { message = "Payment already rejected.", status = payout.Status.ToString() });
        }

        payout.Status = PayoutStatus.Rejected;
        payout.ConfirmedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Payment rejected successfully.",
            payoutId = payout.Id,
            status = payout.Status.ToString(),
            payout.ConfirmedAt
        });
    }

    private static bool CanManagePond(AppUser user, Pond pond)
    {
        return user.Role == UserRole.Admin || pond.OwnerId == user.Id;
    }

    private static decimal CalculateSimpleInterest(decimal principalAmount, DateTime contributionDate, decimal annualRatePercent)
    {
        if (principalAmount <= 0 || annualRatePercent <= 0)
        {
            return 0m;
        }

        var fromDate = contributionDate.Date;
        var toDate = DateTime.UtcNow.Date;
        if (toDate <= fromDate)
        {
            return 0m;
        }

        var elapsedDays = (decimal)(toDate - fromDate).TotalDays;
        var years = elapsedDays / 365m;
        var rate = annualRatePercent / 100m;
        return decimal.Round(principalAmount * rate * years, 2, MidpointRounding.AwayFromZero);
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

    private static string NormalizePhone(string phoneNumber)
    {
        var chars = phoneNumber.Where(char.IsDigit).ToArray();
        return new string(chars);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email.Trim());
            return parsed.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GenerateUserName(string firstName, string lastName)
    {
        var baseName = $"{firstName.Trim()}.{lastName.Trim()}".ToLowerInvariant();
        baseName = new string(baseName.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "user";
        }

        var candidate = baseName;
        var suffix = 1;
        while (await _dbContext.Users.AnyAsync(u => u.UserName == candidate))
        {
            suffix++;
            candidate = $"{baseName}{suffix}";
        }

        return candidate;
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        var all = upper + lower + digits + symbols;

        var chars = new List<char>
        {
            upper[Random.Shared.Next(upper.Length)],
            lower[Random.Shared.Next(lower.Length)],
            digits[Random.Shared.Next(digits.Length)],
            symbols[Random.Shared.Next(symbols.Length)]
        };

        for (var i = chars.Count; i < 10; i++)
        {
            chars.Add(all[Random.Shared.Next(all.Length)]);
        }

        return new string(chars.OrderBy(_ => Random.Shared.Next()).ToArray());
    }

    private async Task<Group?> GetAccessibleGroup(Guid groupId, AppUser loggedInUser)
    {
        return await _dbContext.Groups.FirstOrDefaultAsync(g =>
            g.Id == groupId
            && (
                loggedInUser.Role == UserRole.Admin
                || (loggedInUser.Role == UserRole.GroupManager && g.ManagerId == loggedInUser.Id)
                || (loggedInUser.Role == UserRole.Farmer && _dbContext.GroupMemberships.Any(m => m.GroupId == g.Id && m.UserId == loggedInUser.Id))
            ));
    }
}
