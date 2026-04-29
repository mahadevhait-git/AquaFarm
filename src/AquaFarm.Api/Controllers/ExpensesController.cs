using AquaFarm.Core.Dtos;
using AquaFarm.Core.Entities;
using AquaFarm.Core;
using AquaFarm.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AquaFarm.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private const long MaxBillFileSizeBytes = 10 * 1024 * 1024;
    private const string ExpenseBillsFolder = "expense-bills";
    private const string PondBillsFolder = "pond-bills";
    private const string ExpenseAttachmentsFolder = "expense-attachments";
    private readonly AquaFarmDbContext _dbContext;
    private readonly IWebHostEnvironment _hostEnvironment;

    public ExpensesController(AquaFarmDbContext dbContext, IWebHostEnvironment hostEnvironment)
    {
        _dbContext = dbContext;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? pondId = null)
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

        var expensesQuery = _dbContext.Expenses
            .Include(e => e.Pond)
            .ThenInclude(p => p!.Group)
            .AsQueryable();
        if (pondId.HasValue && pondId.Value != Guid.Empty)
        {
            var selectedPond = await _dbContext.Ponds.Include(p => p.Group).FirstOrDefaultAsync(p => p.Id == pondId.Value);
            if (selectedPond is null)
            {
                return NotFound("Selected pond not found.");
            }

            var canViewSelectedPond =
                loggedInUser.Role == UserRole.Admin
                || selectedPond.OwnerId == loggedInUser.Id
                || (loggedInUser.Role == UserRole.Farmer
                    && selectedPond.GroupId.HasValue
                    && farmerGroupIds.Contains(selectedPond.GroupId.Value));

            if (!canViewSelectedPond)
            {
                return Ok(Array.Empty<ExpenseDto>());
            }

            expensesQuery = expensesQuery.Where(e => e.PondId == pondId.Value);
        }
        else if (loggedInUser.Role != UserRole.Admin)
        {
            expensesQuery = expensesQuery.Where(e =>
                e.CreatedById == loggedInUser.Id
                || e.Pond!.OwnerId == loggedInUser.Id
                || (loggedInUser.Role == UserRole.Farmer
                    && e.Pond!.GroupId.HasValue
                    && farmerGroupIds.Contains(e.Pond.GroupId.Value)));
        }

        var expenses = await expensesQuery
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => new ExpenseDto(
                e.Id,
                e.PondId,
                e.Pond != null ? e.Pond.Name : string.Empty,
                e.Pond != null ? e.Pond.Location : null,
                e.Amount,
                e.Purpose,
                e.ExpenseDate,
                e.BillFileName,
                e.CreatedAt))
            .ToListAsync();

        return Ok(expenses);
    }

    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBillFileSizeBytes)]
    public async Task<IActionResult> Create([FromForm] CreateExpenseForm form)
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

        if (form.Amount <= 0)
        {
            return BadRequest("Expense amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(form.Purpose))
        {
            return BadRequest("Expense purpose is required.");
        }

        if (form.PondId == Guid.Empty)
        {
            return BadRequest("Pond is required.");
        }

        var pond = await _dbContext.Ponds
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.Id == form.PondId);
        if (pond is null)
        {
            return BadRequest("Selected pond does not exist.");
        }

        if (!CanManagePond(loggedInUser, pond))
        {
            return Forbid();
        }

        var uploadFiles = new List<IFormFile>();
        if (form.Bill is not null && form.Bill.Length > 0)
        {
            uploadFiles.Add(form.Bill);
        }
        if (form.Bills is not null && form.Bills.Count > 0)
        {
            uploadFiles.AddRange(form.Bills.Where(f => f is not null && f.Length > 0));
        }

        foreach (var file in uploadFiles)
        {
            if (file.Length > MaxBillFileSizeBytes)
            {
                return BadRequest("Bill file is too large. Maximum allowed size is 10 MB.");
            }
        }

        string? billFileName = null;
        string? billContentType = null;
        string? billStoragePath = null;

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            PondId = form.PondId,
            Amount = form.Amount,
            Purpose = form.Purpose.Trim(),
            ExpenseDate = form.Date.Date,
            BillFileName = billFileName,
            BillContentType = billContentType,
            BillStoragePath = billStoragePath,
            CreatedById = loggedInUser.Id,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Expenses.Add(expense);

        if (uploadFiles.Count > 0)
        {
            var legacyRoot = Path.Combine(_hostEnvironment.ContentRootPath, "uploads", ExpenseBillsFolder);
            Directory.CreateDirectory(legacyRoot);

            var attachmentsRoot = Path.Combine(_hostEnvironment.ContentRootPath, "uploads", ExpenseAttachmentsFolder);
            Directory.CreateDirectory(attachmentsRoot);

            var primaryFile = uploadFiles[0];
            var primaryExtension = Path.GetExtension(primaryFile.FileName);
            var primaryGeneratedFileName = $"{Guid.NewGuid()}{primaryExtension}";
            var primaryAbsolutePath = Path.Combine(legacyRoot, primaryGeneratedFileName);

            await using (var primaryStream = new FileStream(primaryAbsolutePath, FileMode.Create))
            {
                await primaryFile.CopyToAsync(primaryStream);
            }

            billFileName = Path.GetFileName(primaryFile.FileName);
            billContentType = string.IsNullOrWhiteSpace(primaryFile.ContentType)
                ? "application/octet-stream"
                : primaryFile.ContentType;
            billStoragePath = Path.Combine("uploads", ExpenseBillsFolder, primaryGeneratedFileName);

            expense.BillFileName = billFileName;
            expense.BillContentType = billContentType;
            expense.BillStoragePath = billStoragePath;

            foreach (var uploadFile in uploadFiles)
            {
                var extension = Path.GetExtension(uploadFile.FileName);
                var generatedFileName = $"{Guid.NewGuid()}{extension}";
                var absolutePath = Path.Combine(attachmentsRoot, generatedFileName);

                await using (var stream = new FileStream(absolutePath, FileMode.Create))
                {
                    await uploadFile.CopyToAsync(stream);
                }

                var expenseBill = new ExpenseBill
                {
                    Id = Guid.NewGuid(),
                    ExpenseId = expense.Id,
                    FileName = Path.GetFileName(uploadFile.FileName),
                    ContentType = string.IsNullOrWhiteSpace(uploadFile.ContentType) ? "application/octet-stream" : uploadFile.ContentType,
                    StoragePath = Path.Combine("uploads", ExpenseAttachmentsFolder, generatedFileName),
                    UploadedById = loggedInUser.Id,
                    UploadedAt = DateTime.UtcNow
                };
                _dbContext.ExpenseBills.Add(expenseBill);
            }
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new ExpenseDto(
            expense.Id,
            expense.PondId,
            pond.Name,
            pond.Location,
            expense.Amount,
            expense.Purpose,
            expense.ExpenseDate,
            expense.BillFileName,
            expense.CreatedAt));
    }

    [HttpGet("ponds/{pondId:guid}/bills")]
    public async Task<IActionResult> GetPondBills(Guid pondId)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var pond = await _dbContext.Ponds.Include(p => p.Group).FirstOrDefaultAsync(p => p.Id == pondId);
        if (pond is null)
        {
            return NotFound("Selected pond not found.");
        }

        if (!CanManagePond(loggedInUser, pond))
        {
            return Forbid();
        }

        var bills = await _dbContext.PondBills
            .Where(b => b.PondId == pondId)
            .OrderByDescending(b => b.UploadedAt)
            .Select(b => new PondBillDto(
                b.Id,
                b.PondId,
                pond.Name,
                b.FileName,
                b.UploadedAt))
            .ToListAsync();

        return Ok(bills);
    }

    [HttpPost("ponds/{pondId:guid}/bills")]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBillFileSizeBytes)]
    public async Task<IActionResult> UploadPondBill(Guid pondId, [FromForm] UploadPondBillForm form)
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

        var pond = await _dbContext.Ponds.Include(p => p.Group).FirstOrDefaultAsync(p => p.Id == pondId);
        if (pond is null)
        {
            return NotFound("Selected pond not found.");
        }

        if (!CanManagePond(loggedInUser, pond))
        {
            return Forbid();
        }

        if (form.Bill is null || form.Bill.Length == 0)
        {
            return BadRequest("Bill file is required.");
        }

        if (form.Bill.Length > MaxBillFileSizeBytes)
        {
            return BadRequest("Bill file is too large. Maximum allowed size is 10 MB.");
        }

        var uploadRoot = Path.Combine(_hostEnvironment.ContentRootPath, "uploads", PondBillsFolder);
        Directory.CreateDirectory(uploadRoot);

        var extension = Path.GetExtension(form.Bill.FileName);
        var generatedFileName = $"{Guid.NewGuid()}{extension}";
        var absolutePath = Path.Combine(uploadRoot, generatedFileName);
        await using (var stream = new FileStream(absolutePath, FileMode.Create))
        {
            await form.Bill.CopyToAsync(stream);
        }

        var pondBill = new PondBill
        {
            Id = Guid.NewGuid(),
            PondId = pondId,
            FileName = Path.GetFileName(form.Bill.FileName),
            ContentType = string.IsNullOrWhiteSpace(form.Bill.ContentType) ? "application/octet-stream" : form.Bill.ContentType,
            StoragePath = Path.Combine("uploads", PondBillsFolder, generatedFileName),
            UploadedById = loggedInUser.Id,
            UploadedAt = DateTime.UtcNow
        };

        _dbContext.PondBills.Add(pondBill);
        await _dbContext.SaveChangesAsync();

        return Ok(new PondBillDto(
            pondBill.Id,
            pondBill.PondId,
            pond.Name,
            pondBill.FileName,
            pondBill.UploadedAt));
    }

    [HttpGet("pond-bills/{billId:guid}/download")]
    public async Task<IActionResult> DownloadPondBill(Guid billId)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var bill = await _dbContext.PondBills
            .Include(b => b.Pond)
            .ThenInclude(p => p!.Group)
            .FirstOrDefaultAsync(b => b.Id == billId);
        if (bill is null || bill.Pond is null)
        {
            return NotFound("Bill not found.");
        }

        if (!CanManagePond(loggedInUser, bill.Pond))
        {
            return Forbid();
        }

        var absolutePath = Path.Combine(_hostEnvironment.ContentRootPath, bill.StoragePath);
        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound("Bill file is missing.");
        }

        var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, bill.ContentType, bill.FileName);
    }

    [HttpGet("{expenseId:guid}/bill")]
    public async Task<IActionResult> DownloadBill(Guid expenseId)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var expense = await _dbContext.Expenses
            .Include(e => e.Pond)
            .ThenInclude(p => p!.Group)
            .FirstOrDefaultAsync(e => e.Id == expenseId);
        if (expense is null)
        {
            return NotFound("Expense not found.");
        }

        if (!await CanAccessExpense(loggedInUser, expense))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(expense.BillStoragePath) || string.IsNullOrWhiteSpace(expense.BillFileName))
        {
            return NotFound("No bill attachment found for this expense.");
        }

        var absolutePath = Path.Combine(_hostEnvironment.ContentRootPath, expense.BillStoragePath);
        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound("Bill attachment file is missing.");
        }

        var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(
            fileStream: stream,
            contentType: string.IsNullOrWhiteSpace(expense.BillContentType) ? "application/octet-stream" : expense.BillContentType,
            fileDownloadName: expense.BillFileName);
    }

    [HttpDelete("{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid expenseId)
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

        var expense = await _dbContext.Expenses
            .Include(e => e.Pond)
            .ThenInclude(p => p!.Group)
            .FirstOrDefaultAsync(e => e.Id == expenseId);
        if (expense is null)
        {
            return NotFound("Expense not found.");
        }

        if (!await CanAccessExpense(loggedInUser, expense))
        {
            return Forbid();
        }

        var expenseBills = await _dbContext.ExpenseBills
            .Where(b => b.ExpenseId == expenseId)
            .ToListAsync();

        foreach (var bill in expenseBills)
        {
            DeleteFileIfExists(bill.StoragePath);
        }

        if (!string.IsNullOrWhiteSpace(expense.BillStoragePath))
        {
            DeleteFileIfExists(expense.BillStoragePath);
        }

        _dbContext.InvestmentExpenseAudits.Add(new InvestmentExpenseAudit
        {
            Id = Guid.NewGuid(),
            RecordType = "Expense",
            ActionType = "Delete",
            RecordId = expense.Id,
            GroupId = expense.Pond?.GroupId,
            PondId = expense.PondId,
            FarmerId = null,
            OldAmount = expense.Amount,
            NewAmount = null,
            OldValuesJson = JsonSerializer.Serialize(new
            {
                expense.Id,
                expense.PondId,
                GroupId = expense.Pond?.GroupId,
                expense.Amount,
                expense.Purpose,
                expense.ExpenseDate,
                expense.CreatedById,
                expense.CreatedAt
            }),
            NewValuesJson = null,
            PerformedById = loggedInUser.Id,
            PerformedByUserName = loggedInUser.UserName,
            CreatedAt = DateTime.UtcNow
        });

        _dbContext.ExpenseBills.RemoveRange(expenseBills);
        _dbContext.Expenses.Remove(expense);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{expenseId:guid}/bills")]
    public async Task<IActionResult> GetExpenseBills(Guid expenseId)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var expense = await _dbContext.Expenses
            .Include(e => e.Pond)
            .ThenInclude(p => p!.Group)
            .FirstOrDefaultAsync(e => e.Id == expenseId);
        if (expense is null)
        {
            return NotFound("Expense not found.");
        }

        if (!await CanAccessExpense(loggedInUser, expense))
        {
            return Forbid();
        }

        var bills = await _dbContext.ExpenseBills
            .Where(b => b.ExpenseId == expenseId)
            .OrderByDescending(b => b.UploadedAt)
            .Select(b => new ExpenseBillDto(
                b.Id,
                b.ExpenseId,
                b.FileName,
                b.UploadedAt,
                false))
            .ToListAsync();

        // Legacy single-bill fields are kept for backward compatibility.
        // Add legacy row only when there are no explicit ExpenseBills rows,
        // otherwise the first uploaded file appears twice.
        if (bills.Count == 0
            && !string.IsNullOrWhiteSpace(expense.BillFileName)
            && !string.IsNullOrWhiteSpace(expense.BillStoragePath))
        {
            bills.Insert(0, new ExpenseBillDto(
                Guid.Empty,
                expense.Id,
                expense.BillFileName,
                expense.CreatedAt,
                IsLegacy: true));
        }

        return Ok(bills);
    }

    [HttpPost("{expenseId:guid}/bills")]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBillFileSizeBytes)]
    public async Task<IActionResult> UploadExpenseBill(Guid expenseId, [FromForm] UploadExpenseBillForm form)
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

        var expense = await _dbContext.Expenses
            .Include(e => e.Pond)
            .ThenInclude(p => p!.Group)
            .FirstOrDefaultAsync(e => e.Id == expenseId);
        if (expense is null)
        {
            return NotFound("Expense not found.");
        }

        if (!await CanAccessExpense(loggedInUser, expense))
        {
            return Forbid();
        }

        if (form.Bill is null || form.Bill.Length == 0)
        {
            return BadRequest("Bill file is required.");
        }

        if (form.Bill.Length > MaxBillFileSizeBytes)
        {
            return BadRequest("Bill file is too large. Maximum allowed size is 10 MB.");
        }

        var uploadRoot = Path.Combine(_hostEnvironment.ContentRootPath, "uploads", ExpenseAttachmentsFolder);
        Directory.CreateDirectory(uploadRoot);

        var extension = Path.GetExtension(form.Bill.FileName);
        var generatedFileName = $"{Guid.NewGuid()}{extension}";
        var absolutePath = Path.Combine(uploadRoot, generatedFileName);
        await using (var stream = new FileStream(absolutePath, FileMode.Create))
        {
            await form.Bill.CopyToAsync(stream);
        }

        var expenseBill = new ExpenseBill
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FileName = Path.GetFileName(form.Bill.FileName),
            ContentType = string.IsNullOrWhiteSpace(form.Bill.ContentType) ? "application/octet-stream" : form.Bill.ContentType,
            StoragePath = Path.Combine("uploads", ExpenseAttachmentsFolder, generatedFileName),
            UploadedById = loggedInUser.Id,
            UploadedAt = DateTime.UtcNow
        };

        _dbContext.ExpenseBills.Add(expenseBill);
        await _dbContext.SaveChangesAsync();

        return Ok(new ExpenseBillDto(
            expenseBill.Id,
            expenseBill.ExpenseId,
            expenseBill.FileName,
            expenseBill.UploadedAt));
    }

    [HttpGet("expense-bills/{billId:guid}/download")]
    public async Task<IActionResult> DownloadExpenseBill(Guid billId)
    {
        var loggedInUser = await GetLoggedInUser();
        if (loggedInUser is null)
        {
            return Unauthorized("Session is stale. Please logout and login again.");
        }

        var bill = await _dbContext.ExpenseBills
            .Include(b => b.Expense)
            .ThenInclude(e => e!.Pond)
            .ThenInclude(p => p!.Group)
            .FirstOrDefaultAsync(b => b.Id == billId);
        if (bill is null || bill.Expense is null)
        {
            return NotFound("Expense bill not found.");
        }

        if (!await CanAccessExpense(loggedInUser, bill.Expense))
        {
            return Forbid();
        }

        var absolutePath = Path.Combine(_hostEnvironment.ContentRootPath, bill.StoragePath);
        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound("Bill file is missing.");
        }

        var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, bill.ContentType, bill.FileName);
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

    private static bool CanManagePond(AppUser user, Pond pond)
    {
        if (user.Role == UserRole.Admin)
        {
            return true;
        }

        if (pond.OwnerId == user.Id)
        {
            return true;
        }

        return false;
    }

    private async Task<bool> CanAccessExpense(AppUser user, Expense expense)
    {
        if (user.Role == UserRole.Admin)
        {
            return true;
        }

        if (expense.CreatedById == user.Id)
        {
            return true;
        }

        if (expense.Pond?.OwnerId == user.Id)
        {
            return true;
        }

        if (user.Role == UserRole.Farmer && expense.Pond?.GroupId is Guid pondGroupId)
        {
            var isGroupMember = await _dbContext.GroupMemberships
                .AnyAsync(m => m.UserId == user.Id && m.GroupId == pondGroupId);
            if (isGroupMember)
            {
                return true;
            }
        }

        return false;
    }

    private void DeleteFileIfExists(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var absolutePath = Path.Combine(_hostEnvironment.ContentRootPath, relativePath);
        if (System.IO.File.Exists(absolutePath))
        {
            System.IO.File.Delete(absolutePath);
        }
    }
}

public class CreateExpenseForm
{
    public Guid PondId { get; set; }
    public decimal Amount { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public IFormFile? Bill { get; set; }
    public List<IFormFile>? Bills { get; set; }
}

public class UploadPondBillForm
{
    public IFormFile? Bill { get; set; }
}

public class UploadExpenseBillForm
{
    public IFormFile? Bill { get; set; }
}
