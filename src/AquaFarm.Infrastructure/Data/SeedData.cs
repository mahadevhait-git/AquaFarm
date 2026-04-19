using AquaFarm.Core.Entities;
using AquaFarm.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AquaFarm.Infrastructure.Data;

public static class SeedData
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AquaFarmDbContext>();
        context.Database.EnsureCreated();
        EnsureContributionSchema(context);

        var admin = context.Users.FirstOrDefault(u => u.PhoneNumber == "9000000000");
        if (admin is null)
        {
            admin = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = "admin",
                FirstName = "System",
                LastName = "Admin",
                Address = "Platform HQ",
                Email = "admin@aquafarm.local",
                PhoneNumber = "9000000000",
                PasswordHash = "admin123",
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(admin);
        }
        else
        {
            admin.UserName = "admin";
            admin.FirstName = "System";
            admin.LastName = "Admin";
            admin.Address = "Platform HQ";
            admin.Email = "admin@aquafarm.local";
            admin.PasswordHash = "admin123";
            admin.Role = UserRole.Admin;
        }

        var extraAdmins = context.Users.Where(u => u.Role == UserRole.Admin && u.Id != admin.Id).ToList();
        if (extraAdmins.Count > 0)
        {
            context.Users.RemoveRange(extraAdmins);
        }

        var farmer = context.Users.FirstOrDefault(u => u.PhoneNumber == "9000000001");
        if (farmer is null)
        {
            farmer = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = "farmer1",
                FirstName = "Farmer",
                LastName = "One",
                Address = "Northeast Block",
                Email = "farmer1@aquafarm.local",
                PhoneNumber = "9000000001",
                PasswordHash = "password123",
                Role = UserRole.Farmer,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(farmer);
        }
        else
        {
            farmer.UserName = "farmer1";
            farmer.FirstName = "Farmer";
            farmer.LastName = "One";
            farmer.Address = "Northeast Block";
            farmer.Email = "farmer1@aquafarm.local";
            farmer.PasswordHash = "password123";
            farmer.Role = UserRole.Farmer;
        }

        var groupManager = context.Users.FirstOrDefault(u => u.PhoneNumber == "9000000002");
        if (groupManager is null)
        {
            groupManager = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = "manager1",
                FirstName = "Group",
                LastName = "Manager",
                Address = "Central Zone",
                Email = "manager1@aquafarm.local",
                PhoneNumber = "9000000002",
                PasswordHash = "password123",
                Role = UserRole.GroupManager,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(groupManager);
        }
        else
        {
            groupManager.UserName = "manager1";
            groupManager.FirstName = "Group";
            groupManager.LastName = "Manager";
            groupManager.Address = "Central Zone";
            groupManager.Email = "manager1@aquafarm.local";
            groupManager.PasswordHash = "password123";
            groupManager.Role = UserRole.GroupManager;
        }

        var ishaniManager = context.Users.FirstOrDefault(u => u.UserName == "ishani.hait");
        if (ishaniManager is null)
        {
            ishaniManager = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = "ishani.hait",
                FirstName = "Ishani",
                LastName = "Hait",
                Address = "Central Zone",
                Email = "ishani.hait@aquafarm.local",
                PhoneNumber = "9000000003",
                PasswordHash = "password123",
                Role = UserRole.GroupManager,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(ishaniManager);
        }
        else
        {
            ishaniManager.FirstName = "Ishani";
            ishaniManager.LastName = "Hait";
            ishaniManager.Address = "Central Zone";
            ishaniManager.Email = "ishani.hait@aquafarm.local";
            ishaniManager.PhoneNumber = string.IsNullOrWhiteSpace(ishaniManager.PhoneNumber) ? "9000000003" : ishaniManager.PhoneNumber;
            ishaniManager.PasswordHash = "password123";
            ishaniManager.Role = UserRole.GroupManager;
        }

        context.SaveChanges();

        var group = context.Groups.FirstOrDefault(g => g.Name == "Green Pond Cooperative");
        if (group is null)
        {
            group = new Group
            {
                Id = Guid.NewGuid(),
                Name = "Green Pond Cooperative",
                Description = "Ten smallholder farmers sharing group finance records.",
                ManagerId = groupManager.Id,
                CreatedAt = DateTime.UtcNow
            };
            context.Groups.Add(group);
            context.SaveChanges();
        }
        else
        {
            group.ManagerId = groupManager.Id;
        }

        var membershipExists = context.GroupMemberships.Any(m => m.GroupId == group.Id && m.UserId == farmer.Id);
        if (!membershipExists)
        {
            context.GroupMemberships.Add(new GroupMembership
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                UserId = farmer.Id,
                Role = MembershipRole.Member,
                JoinedAt = DateTime.UtcNow
            });
        }

        var pond = context.Ponds.FirstOrDefault(p => p.Name == "Pond A1" && p.OwnerId == farmer.Id);
        if (pond is null)
        {
            pond = new Pond
            {
                Id = Guid.NewGuid(),
                Name = "Pond A1",
                Location = "Northeast Block",
                OwnerId = farmer.Id,
                GroupId = group.Id,
                CreatedAt = DateTime.UtcNow
            };
            context.Ponds.Add(pond);
            context.SaveChanges();
        }

        if (!context.Transactions.Any(t => t.PondId == pond.Id))
        {
            context.Transactions.AddRange(
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Type = TransactionType.Investment,
                    Category = "Fingerlings",
                    Amount = 4500m,
                    Date = DateTime.UtcNow.AddDays(-20),
                    PondId = pond.Id,
                    CreatedById = farmer.Id,
                    Notes = "First batch investment",
                    CreatedAt = DateTime.UtcNow
                },
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Type = TransactionType.Expense,
                    Category = "Feed",
                    Amount = 1800m,
                    Date = DateTime.UtcNow.AddDays(-10),
                    PondId = pond.Id,
                    CreatedById = farmer.Id,
                    Notes = "Weekly feed purchase",
                    CreatedAt = DateTime.UtcNow
                }
            );
        }

        var hasSeedLoan = context.Loans.Any(l => l.LenderId == admin.Id && l.BorrowerId == farmer.Id);
        if (!hasSeedLoan)
        {
            var seededContribution = 10000m;
            context.Loans.Add(new Loan
            {
                Id = Guid.NewGuid(),
                LenderId = admin.Id,
                BorrowerId = farmer.Id,
                GroupId = group.Id,
                PrincipalAmount = seededContribution,
                InterestRate = 0m,
                InterestType = InterestType.Simple,
                TermMonths = 0,
                OutstandingBalance = seededContribution,
                StartDate = DateTime.UtcNow.AddDays(-15),
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            });

            context.CapitalTransactions.Add(new CapitalTransaction
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                FarmerId = farmer.Id,
                ContributionDate = DateTime.UtcNow.AddDays(-15),
                Amount = seededContribution,
                CreatedAt = DateTime.UtcNow
            });
        }

        context.SaveChanges();
    }

    private static void EnsureContributionSchema(AquaFarmDbContext context)
    {
        context.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('Loans', 'GroupId') IS NULL
BEGIN
    ALTER TABLE [Loans] ADD [GroupId] uniqueidentifier NULL;
END;");

        context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[CapitalTransactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [CapitalTransactions](
        [Id] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [FarmerId] uniqueidentifier NOT NULL,
        [ContributionDate] datetime2 NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CapitalTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CapitalTransactions_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CapitalTransactions_Users_FarmerId] FOREIGN KEY ([FarmerId]) REFERENCES [Users]([Id]) ON DELETE NO ACTION
    );
END;");

        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Loans_GroupId_BorrowerId' AND object_id = OBJECT_ID('Loans'))
BEGIN
    CREATE INDEX [IX_Loans_GroupId_BorrowerId] ON [Loans]([GroupId], [BorrowerId]);
END;");

        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Loans_GroupId_BorrowerId_GroupTotals' AND object_id = OBJECT_ID('Loans'))
BEGIN
    CREATE UNIQUE INDEX [UX_Loans_GroupId_BorrowerId_GroupTotals]
    ON [Loans]([GroupId], [BorrowerId])
    WHERE [GroupId] IS NOT NULL;
END;");

        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CapitalTransactions_GroupId_FarmerId_ContributionDate' AND object_id = OBJECT_ID('CapitalTransactions'))
BEGIN
    CREATE INDEX [IX_CapitalTransactions_GroupId_FarmerId_ContributionDate]
    ON [CapitalTransactions]([GroupId], [FarmerId], [ContributionDate]);
END;");

        context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[Expenses]', N'U') IS NULL
BEGIN
    CREATE TABLE [Expenses](
        [Id] uniqueidentifier NOT NULL,
        [PondId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Purpose] nvarchar(300) NOT NULL,
        [ExpenseDate] datetime2 NOT NULL,
        [BillFileName] nvarchar(260) NULL,
        [BillContentType] nvarchar(120) NULL,
        [BillStoragePath] nvarchar(500) NULL,
        [CreatedById] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Expenses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Expenses_Ponds_PondId] FOREIGN KEY ([PondId]) REFERENCES [Ponds]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Expenses_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Users]([Id]) ON DELETE NO ACTION
    );
END;");

        context.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('Expenses', 'PondId') IS NULL
BEGIN
    ALTER TABLE [Expenses] ADD [PondId] uniqueidentifier NULL;
END;");

        context.Database.ExecuteSqlRaw(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'PondId' AND is_nullable = 1)
BEGIN
    DECLARE @defaultPondId uniqueidentifier;
    SELECT TOP(1) @defaultPondId = [Id] FROM [Ponds] ORDER BY [CreatedAt];
    IF @defaultPondId IS NOT NULL
    BEGIN
        UPDATE [Expenses] SET [PondId] = @defaultPondId WHERE [PondId] IS NULL;
    END
    IF EXISTS (SELECT 1 FROM [Expenses] WHERE [PondId] IS NULL)
    BEGIN
        DELETE FROM [Expenses] WHERE [PondId] IS NULL;
    END
    ALTER TABLE [Expenses] ALTER COLUMN [PondId] uniqueidentifier NOT NULL;
END;");

        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expenses_Ponds_PondId')
BEGIN
    ALTER TABLE [Expenses]
    ADD CONSTRAINT [FK_Expenses_Ponds_PondId] FOREIGN KEY ([PondId]) REFERENCES [Ponds]([Id]) ON DELETE NO ACTION;
END;");

        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_CreatedById_ExpenseDate' AND object_id = OBJECT_ID('Expenses'))
BEGIN
    CREATE INDEX [IX_Expenses_CreatedById_ExpenseDate] ON [Expenses]([CreatedById], [ExpenseDate]);
END;");

        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_PondId_ExpenseDate' AND object_id = OBJECT_ID('Expenses'))
BEGIN
    CREATE INDEX [IX_Expenses_PondId_ExpenseDate] ON [Expenses]([PondId], [ExpenseDate]);
END;");

        context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[PondBills]', N'U') IS NULL
BEGIN
    CREATE TABLE [PondBills](
        [Id] uniqueidentifier NOT NULL,
        [PondId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [ContentType] nvarchar(120) NOT NULL,
        [StoragePath] nvarchar(500) NOT NULL,
        [UploadedById] uniqueidentifier NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PondBills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PondBills_Ponds_PondId] FOREIGN KEY ([PondId]) REFERENCES [Ponds]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PondBills_Users_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Users]([Id]) ON DELETE NO ACTION
    );
END;");

        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PondBills_PondId_UploadedAt' AND object_id = OBJECT_ID('PondBills'))
BEGIN
    CREATE INDEX [IX_PondBills_PondId_UploadedAt] ON [PondBills]([PondId], [UploadedAt]);
END;");

        context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[ExpenseBills]', N'U') IS NULL
BEGIN
    CREATE TABLE [ExpenseBills](
        [Id] uniqueidentifier NOT NULL,
        [ExpenseId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [ContentType] nvarchar(120) NOT NULL,
        [StoragePath] nvarchar(500) NOT NULL,
        [UploadedById] uniqueidentifier NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ExpenseBills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpenseBills_Expenses_ExpenseId] FOREIGN KEY ([ExpenseId]) REFERENCES [Expenses]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ExpenseBills_Users_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Users]([Id]) ON DELETE NO ACTION
    );
END;");

        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExpenseBills_ExpenseId_UploadedAt' AND object_id = OBJECT_ID('ExpenseBills'))
BEGIN
    CREATE INDEX [IX_ExpenseBills_ExpenseId_UploadedAt] ON [ExpenseBills]([ExpenseId], [UploadedAt]);
END;");
    }
}
