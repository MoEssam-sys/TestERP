using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using One.Application.Repos.Identity;
using One.Application.Services;
using One.Application.Services.Subscriptions;
using One.Domain.Constants;
using One.Domain.Entities.Identity;
using One.Domain.Tenancy;
using One.Infrastructure.DbContext.UsersDbContext;
using One.Infrastructure.Interfaces;
using One.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using One.API.Authorization;
using One.API.Health;
using One.API.Middleware;
using One.API.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Text;
using One.API.Services.Logging;

namespace One.API.Configuration
{
    public static class ApiConfig
    {
        public static IServiceCollection AddOneAPI(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            // Controllers
            services.AddControllers();
            services.AddProblemDetails();
            services.AddExceptionHandler<GlobalExceptionHandler>();

            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.AddSingleton<AppErrorLogQueue>();
            services.AddSingleton<IAppErrorLogQueue>(provider => provider.GetRequiredService<AppErrorLogQueue>());
            services.AddSingleton<ILoggerProvider, PersistentErrorLoggerProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<AppErrorLogQueue>());
            services.AddScoped<ITenantContext, One.API.Services.HttpTenantContext>();

            var userConnection = GetRequiredValue(
                configuration.GetConnectionString("UserConnection"),
                "ConnectionStrings:UserConnection");
            _ = GetRequiredValue(
                configuration.GetConnectionString("MainConnection"),
                "ConnectionStrings:MainConnection");
            var jwtSecret = GetRequiredValue(configuration["JWT:Secret"], "JWT:Secret");
            var validIssuer = GetRequiredValue(configuration["JWT:ValidIssuer"], "JWT:ValidIssuer");
            var validAudience = GetRequiredValue(configuration["JWT:ValidAudience"], "JWT:ValidAudience");
            _ = GetRequiredValue(configuration["Security:OtpPepper"], "Security:OtpPepper");
            ValidateSecurityConfiguration(configuration, environment, jwtSecret);

            // DbContext
            services.AddDbContext<UsersDbContext>(options =>
            {
                options.UseSqlServer(
                    userConnection,
                    b => b.MigrationsAssembly(typeof(UsersDbContext).Assembly.FullName));
            });

            // Identity
            services.AddIdentity<UserData, IdentityRole>(options =>
                {
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = configuration.GetValue<int>(
                        "Authentication:Lockout:MaxFailedAttempts",
                        5);
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
                        configuration.GetValue<int>("Authentication:Lockout:Minutes", 15));
                })
                .AddEntityFrameworkStores<UsersDbContext>()
                .AddDefaultTokenProviders();
            services.Configure<DataProtectionTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromMinutes(configuration.GetValue<int>(
                    "Authentication:PasswordReset:TokenMinutes",
                    20));
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    AuthorizationPolicies.ManageUsers,
                    policy => policy.RequireRole(SystemRoles.SuperAdmin, SystemRoles.CompanyAdmin));
                options.AddPolicy(
                    AuthorizationPolicies.ManageCompanies,
                    policy => policy.RequireRole(SystemRoles.SuperAdmin));
            });

            // JWT Auth - RequireHttpsMetadata from config
            var requireHttps = configuration.GetValue<bool>("Security:RequireHttpsMetadata", true);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.SaveToken = false;
                options.RequireHttpsMetadata = requireHttps;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = validIssuer,
                    ValidAudience = validAudience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret))
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/OneHubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ValidateSessionAsync
                };
            });

            // CORS - Configurable origins from CorsSettings section
            var corsSettings = configuration.GetSection("CorsSettings");
            var allowedOrigins = corsSettings.GetSection("AllowedOrigins").Get<string[]>() ?? [];
            var allowCredentials = corsSettings.GetValue<bool>("AllowCredentials", false);

            services.AddCors(options =>
            {
                options.AddPolicy("MyCors", policy =>
                {
                    if (allowedOrigins.Length > 0)
                        policy.WithOrigins(allowedOrigins);
                    else if (environment.IsDevelopment())
                        policy.AllowAnyOrigin();

                    policy.AllowAnyMethod();
                    policy.AllowAnyHeader();

                    if (allowCredentials && allowedOrigins.Length > 0)
                        policy.AllowCredentials();
                });
            });

            // Auth Repository & Service (UnitOfWork removed)
            services.AddScoped<IAuthRepository, AuthRepository>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IPermissionCatalogService, PermissionCatalogService>();
            services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
            services.AddScoped<ICompanySubscriptionService, CompanySubscriptionService>();
            services.AddScoped<ICompanyProvisioningService, CompanyProvisioningService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddSignalR();
            services.AddHostedService<One.API.Services.SubscriptionStatusWorker>();
            services.AddHostedService<DataRetentionWorker>();
            services.AddHealthChecks()
                .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
                .AddCheck<DatabaseReadinessHealthCheck>("databases", tags: ["ready"]);
            AddRateLimiting(services, configuration);
            ConfigureForwardedHeaders(services, configuration);

            // Localization
            services.AddSingleton<One.Domain.Localization.ITranslator, One.API.Services.ServerTranslator>();

            return services;
        }

        private static string GetRequiredValue(string? value, string key)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("${", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Required configuration '{key}' is missing. Use User Secrets locally or an environment variable when deployed.");
            }

            return value;
        }

        private static void ValidateSecurityConfiguration(
            IConfiguration configuration,
            IHostEnvironment environment,
            string jwtSecret)
        {
            if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
                throw new InvalidOperationException("JWT:Secret must contain at least 32 bytes.");

            var otpPepper = configuration["Security:OtpPepper"]!;
            if (Encoding.UTF8.GetByteCount(otpPepper) < 32)
                throw new InvalidOperationException("Security:OtpPepper must contain at least 32 bytes.");

            RequireRange(configuration, "Authentication:AccessTokenMinutes", 5, 60);
            RequireRange(configuration, "Authentication:RefreshTokenDays", 1, 90);
            RequireRange(configuration, "Authentication:Lockout:MaxFailedAttempts", 3, 10);
            RequireRange(configuration, "Authentication:Lockout:Minutes", 5, 1440);
            RequireRange(configuration, "Authentication:Otp:CodeLength", 6, 8);
            RequireRange(configuration, "Authentication:Otp:LifetimeMinutes", 2, 10);
            RequireRange(configuration, "Authentication:Otp:ResendCooldownSeconds", 30, 300);
            RequireRange(configuration, "Authentication:Otp:MaxAttempts", 3, 10);
            RequireRange(configuration, "Authentication:PasswordReset:TokenMinutes", 10, 120);
            RequireRange(configuration, "Monitoring:ClientLogRetentionDays", 1, 365);
            RequireRange(configuration, "Monitoring:ServerLogRetentionDays", 1, 90);
            RequireRange(configuration, "Monitoring:DeleteBatchSize", 100, 10000);
            RequireRange(configuration, "Monitoring:MaxDeleteBatchesPerRun", 1, 100);
            RequireRange(configuration, "RateLimiting:WindowSeconds", 1, 3600);
            RequireRange(configuration, "RateLimiting:PermitLimit", 10, 10000);
            RequireRange(configuration, "RateLimiting:LoginPermitLimit", 1, 100);
            RequireRange(configuration, "RateLimiting:OtpRequestPermitLimit", 1, 20);
            RequireRange(configuration, "RateLimiting:OtpVerifyPermitLimit", 1, 100);

            var passwordResetBaseUrl = GetRequiredValue(
                configuration["Authentication:PasswordReset:ClientBaseUrl"],
                "Authentication:PasswordReset:ClientBaseUrl");
            if (!Uri.TryCreate(passwordResetBaseUrl, UriKind.Absolute, out var passwordResetUri) ||
                (passwordResetUri.Scheme != Uri.UriSchemeHttps &&
                 passwordResetUri.Scheme != Uri.UriSchemeHttp))
            {
                throw new InvalidOperationException(
                    "Authentication:PasswordReset:ClientBaseUrl must be an absolute HTTP or HTTPS URL.");
            }
            if (environment.IsProduction() && passwordResetUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "Authentication:PasswordReset:ClientBaseUrl must use HTTPS in production.");
            }

            var reverseProxyEnabled = configuration.GetValue<bool>("ReverseProxy:Enabled");
            var knownProxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
            if (reverseProxyEnabled && knownProxies.Length == 0)
                throw new InvalidOperationException("ReverseProxy:KnownProxies is required when reverse proxy support is enabled.");
            foreach (var proxy in knownProxies)
            {
                if (!IPAddress.TryParse(proxy, out _))
                    throw new InvalidOperationException($"ReverseProxy:KnownProxies contains invalid address '{proxy}'.");
            }

            if (!environment.IsProduction())
                return;

            var origins = configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? [];
            if (origins.Length == 0)
                throw new InvalidOperationException("CorsSettings:AllowedOrigins is required in production.");
            if (!configuration.GetValue<bool>("Security:RequireHttpsMetadata"))
                throw new InvalidOperationException("Security:RequireHttpsMetadata must be enabled in production.");

            _ = GetRequiredValue(configuration["Email:SmtpHost"], "Email:SmtpHost");
            _ = GetRequiredValue(configuration["Email:SmtpUser"], "Email:SmtpUser");
            _ = GetRequiredValue(configuration["Email:SmtpPass"], "Email:SmtpPass");
            _ = GetRequiredValue(configuration["Email:FromEmail"], "Email:FromEmail");
        }

        private static void RequireRange(
            IConfiguration configuration,
            string key,
            int minimum,
            int maximum)
        {
            var value = configuration.GetValue<int?>(key)
                ?? throw new InvalidOperationException($"Required configuration '{key}' is missing.");
            if (value < minimum || value > maximum)
                throw new InvalidOperationException($"Configuration '{key}' must be between {minimum} and {maximum}.");
        }

        private static void AddRateLimiting(
            IServiceCollection services,
            IConfiguration configuration)
        {
            var window = TimeSpan.FromSeconds(configuration.GetValue<int>("RateLimiting:WindowSeconds"));
            var globalPermitLimit = configuration.GetValue<int>("RateLimiting:PermitLimit");
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        $"global:{ClientAddress(context)}",
                        _ => FixedWindow(globalPermitLimit, window)));
                options.AddPolicy("login", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        $"login:{ClientAddress(context)}",
                        _ => FixedWindow(
                            configuration.GetValue<int>("RateLimiting:LoginPermitLimit"),
                            window)));
                options.AddPolicy("otp-request", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        $"otp-request:{ClientAddress(context)}",
                        _ => FixedWindow(
                            configuration.GetValue<int>("RateLimiting:OtpRequestPermitLimit"),
                            window)));
                options.AddPolicy("otp-verify", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        $"otp-verify:{ClientAddress(context)}",
                        _ => FixedWindow(
                            configuration.GetValue<int>("RateLimiting:OtpVerifyPermitLimit"),
                            window)));
                options.AddPolicy("refresh", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        $"refresh:{ClientAddress(context)}",
                        _ => FixedWindow(
                            configuration.GetValue<int>("RateLimiting:LoginPermitLimit"),
                            window)));
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.ContentType = "application/problem+json";
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new
                        {
                            status = StatusCodes.Status429TooManyRequests,
                            title = "Too many requests. Please try again later."
                        },
                        cancellationToken);
                };
            });
        }

        private static FixedWindowRateLimiterOptions FixedWindow(int permitLimit, TimeSpan window) => new()
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true
        };

        private static string ClientAddress(HttpContext context) =>
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private static void ConfigureForwardedHeaders(
            IServiceCollection services,
            IConfiguration configuration)
        {
            if (!configuration.GetValue<bool>("ReverseProxy:Enabled"))
                return;

            var knownProxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                foreach (var proxy in knownProxies)
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
            });
        }

        private static async Task ValidateSessionAsync(TokenValidatedContext context)
        {
            var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sessionIdValue = context.Principal?.FindFirst("sid")?.Value;
            if (string.IsNullOrWhiteSpace(userId) ||
                !Guid.TryParse(sessionIdValue, out var sessionId))
            {
                context.Fail("The session is invalid.");
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<UsersDbContext>();
            var now = DateTime.UtcNow;
            var valid = await dbContext.UserSessions
                .AsNoTracking()
                .AnyAsync(session =>
                    session.Id == sessionId &&
                    session.UserId == userId &&
                    session.RevokedAtUtc == null &&
                    session.ExpiresAtUtc > now &&
                    session.User.IsActive &&
                    session.User.SecurityStamp == session.SecurityStamp &&
                    (session.User.CompanyId == null || session.User.Company!.IsActive),
                    context.HttpContext.RequestAborted);
            if (!valid)
                context.Fail("The session is invalid or revoked.");
        }

        public static async Task SeedRolesAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            // Auto-Migrate ONLY if database exists
            var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

            // Check if we can connect (Database must exist)
            if (!await context.Database.CanConnectAsync())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogCritical("Users Database does not exist! Please create the database manually using the connection string in configuration. The application will not create it automatically.");
                // We stop here to match requirement "Stop if UsersDb doesn't exist"
                // Throwing exception ensures the app doesn't start in a broken state
                throw new Exception("Users Database does not exist. Please create it manually.");
            }

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogCritical(
                    "The Users database has {Count} pending migrations. Apply migrations as a separate deployment step before starting the API.",
                    pendingMigrations.Count());
                throw new InvalidOperationException(
                    "The Users database schema is not current. Apply migrations before starting the API.");
            }

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var roles = new[] { "Admin", "User", "SuperAdmin", "CompanyAdmin", "Tester" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            await PermissionCatalogSeeder.SeedAsync(context);
        }
    }
}
