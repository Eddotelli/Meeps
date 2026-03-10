using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace API.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating ApplicationDbContext instances.
/// Used by EF Core tools (migrations) without starting the full application.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Get connection string from command line argument (--connection)
        // Or fallback to environment variable or default local connection
        var connectionString = GetConnectionString(args);

        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private string GetConnectionString(string[] args)
    {
        // Check if connection string is provided via --connection argument
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--connection" && !string.IsNullOrEmpty(args[i + 1]))
            {
                return args[i + 1];
            }
        }

        // Fallback to environment variable
        var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrEmpty(envConnectionString))
        {
            return envConnectionString;
        }

        // Default local development connection string
        return "Server=(localdb)\\mssqllocaldb;Database=MeepsDb;Trusted_Connection=True;MultipleActiveResultSets=true";
    }
}
