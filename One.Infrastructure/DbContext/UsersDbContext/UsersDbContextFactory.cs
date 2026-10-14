using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace One.Infrastructure.DbContext.UsersDbContext
{
    public sealed class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
    {
        public UsersDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                TryGetCliConnection(args)
                ?? TryGetEnvConnection()
                ?? TryGetConnectionFromApiAppSettings()
                ?? throw new InvalidOperationException(
                    "UsersDbContext connection string not provided. " +
                    "Set ONEERP_USERS_CONNECTION env var or ensure One.API/appsettings.json has ConnectionStrings:UserConnection.");

            var options = new DbContextOptionsBuilder<UsersDbContext>()
                .UseSqlServer(connectionString, sql =>
                {
                    // Ensures migrations are placed/used from Infrastructure assembly
                    sql.MigrationsAssembly(typeof(UsersDbContext).Assembly.FullName);
                })
                .Options;

            return new UsersDbContext(options);
        }

        private static string? TryGetCliConnection(string[] args)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--connection", StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        private static string? TryGetEnvConnection()
            => Environment.GetEnvironmentVariable("ONEERP_USERS_CONNECTION")
                ?? Environment.GetEnvironmentVariable("UserConnection");

        private static string? TryGetConnectionFromApiAppSettings()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (dir != null)
            {
                var apiDir = Path.Combine(dir.FullName, "One.API");
                var appSettingsPath = Path.Combine(apiDir, "appsettings.json");

                if (File.Exists(appSettingsPath))
                {
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(apiDir)
                        .AddJsonFile("appsettings.json", optional: false)
                        .AddJsonFile("appsettings.Development.json", optional: true)
                        .AddEnvironmentVariables()
                        .Build();

                    return configuration.GetConnectionString("UserConnection");
                }

                dir = dir.Parent;
            }

            return null;
        }
    }
}
