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

        if (HasRequiredUserColumns(context) == false)
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        }

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
            context.Loans.Add(new Loan
            {
                Id = Guid.NewGuid(),
                LenderId = admin.Id,
                BorrowerId = farmer.Id,
                PrincipalAmount = 10000m,
                InterestRate = 8.0m,
                InterestType = InterestType.Simple,
                TermMonths = 6,
                OutstandingBalance = 10000m,
                StartDate = DateTime.UtcNow.AddDays(-15),
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        context.SaveChanges();
    }

    private static bool? HasRequiredUserColumns(AquaFarmDbContext context)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            var requiredColumns = new[] { "PhoneNumber", "FirstName", "LastName", "Address" };
            foreach (var column in requiredColumns)
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = '{column}'";
                var result = command.ExecuteScalar();
                var count = Convert.ToInt32(result);
                if (count == 0)
                {
                    return false;
                }
            }

            using var groupCommand = connection.CreateCommand();
            groupCommand.CommandText =
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Groups' AND COLUMN_NAME = 'ManagerId'";
            var groupResult = groupCommand.ExecuteScalar();
            var groupCount = Convert.ToInt32(groupResult);
            if (groupCount == 0)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return null;
        }
    }
}
