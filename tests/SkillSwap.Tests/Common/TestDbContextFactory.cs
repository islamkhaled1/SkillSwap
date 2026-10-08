using Microsoft.EntityFrameworkCore;
using SkillSwap.Infrastructure.Persistence;

namespace SkillSwap.Tests.Common;

public static class TestDbContextFactory
{
    public static ApplicationDbContext CreateInMemoryContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public static ApplicationDbContext CreateSqlServerContext(string connectionString = "Server=localhost;Database=SkillSwap_IntegrationTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;")
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
