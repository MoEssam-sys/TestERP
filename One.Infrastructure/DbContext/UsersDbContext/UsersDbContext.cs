using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using One.Domain.Entities.Identity;
using One.Infrastructure.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace One.Infrastructure.DbContext.UsersDbContext
{
    public class UsersDbContext : IdentityDbContext<UserData>
    {
        public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options)
        {
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<OtpChallenge> OtpChallenges { get; set; }
        public DbSet<AppLog> AppLogs { get; set; }
        public DbSet<AppModule> AppModules { get; set; }
        public DbSet<AppPage> AppPages { get; set; }
        public DbSet<PermissionAction> PermissionActions { get; set; }
        public DbSet<AppPageAction> AppPageActions { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<PlanPermission> PlanPermissions { get; set; }
        public DbSet<CompanySubscription> CompanySubscriptions { get; set; }
        public DbSet<CompanyEntitlement> CompanyEntitlements { get; set; }
        public DbSet<SubscriptionChangeLog> SubscriptionChangeLogs { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<CompanyPermissionVersion> CompanyPermissionVersions { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Company>()
                .HasIndex(c => c.Code)
                .IsUnique();

            builder.Entity<Company>()
                .Property(c => c.IsActive)
                .HasDefaultValue(true)
                .ValueGeneratedOnAdd();

            builder.Entity<UserData>()
                .HasOne(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            ConfigurePermissionCatalog(builder);
            ConfigureSubscriptionManagement(builder);
            ConfigureEffectivePermissions(builder);
            ConfigureAuthenticationSecurity(builder);

            // =========================================================================
            // Seeding Roles (Deterministic IDs & Stamps)
            // =========================================================================
            // Use static constants to prevent PendingModelChangesWarning
            builder.Entity<IdentityRole>().HasData(
                new IdentityRole { Id = SeedConstants.SuperAdminRoleId, Name = "SuperAdmin", NormalizedName = "SUPERADMIN", ConcurrencyStamp = SeedConstants.SuperAdminRoleConcurrencyStamp },
                new IdentityRole { Id = SeedConstants.CompanyAdminRoleId, Name = "CompanyAdmin", NormalizedName = "COMPANYADMIN", ConcurrencyStamp = SeedConstants.CompanyAdminRoleConcurrencyStamp },
                new IdentityRole { Id = SeedConstants.UserRoleId, Name = "User", NormalizedName = "USER", ConcurrencyStamp = SeedConstants.UserRoleConcurrencyStamp },
                new IdentityRole { Id = SeedConstants.TesterRoleId, Name = "Tester", NormalizedName = "TESTER", ConcurrencyStamp = SeedConstants.TesterRoleConcurrencyStamp }
            );

            // =========================================================================
            // Seeding Super Admin
            // =========================================================================
            var superAdminUser = new UserData
            {
                Id = SeedConstants.SuperAdminUserId,
                UserName = "loai.asp97@gmail.com",
                NormalizedUserName = "LOAI.ASP97@GMAIL.COM",
                Email = "loai.asp97@gmail.com",
                NormalizedEmail = "LOAI.ASP97@GMAIL.COM",
                EmailConfirmed = true,
                FirstName = "Super",
                LastName = "Admin",
                CompanyId = null, // Global
                SecurityStamp = SeedConstants.SuperAdminSecurityStamp,
                ConcurrencyStamp = SeedConstants.SuperAdminConcurrencyStamp,
                LockoutEnabled = true
            };

            builder.Entity<UserData>().HasData(superAdminUser);

            // Assign SuperAdmin Role
            builder.Entity<IdentityUserRole<string>>().HasData(new IdentityUserRole<string>
            {
                RoleId = SeedConstants.SuperAdminRoleId,
                UserId = SeedConstants.SuperAdminUserId
            });
        }

        private static void ConfigurePermissionCatalog(ModelBuilder builder)
        {
            builder.Entity<AppModule>(entity =>
            {
                entity.ToTable("AppModules");
                entity.HasKey(module => module.Id);
                entity.HasIndex(module => module.Key).IsUnique();
                entity.HasIndex(module => new
                {
                    module.IsActive,
                    module.IsAssignableToPlans,
                    module.SortOrder
                });
            });

            builder.Entity<AppPage>(entity =>
            {
                entity.ToTable("AppPages");
                entity.HasKey(page => page.Id);
                entity.HasIndex(page => page.Key).IsUnique();
                entity.HasIndex(page => page.Route).IsUnique();
                entity.HasIndex(page => new { page.AppModuleId, page.IsActive, page.SortOrder });
                entity.HasOne(page => page.AppModule)
                    .WithMany(module => module.Pages)
                    .HasForeignKey(page => page.AppModuleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PermissionAction>(entity =>
            {
                entity.ToTable("PermissionActions");
                entity.HasKey(action => action.Id);
                entity.HasIndex(action => action.Key).IsUnique();
                entity.HasIndex(action => new { action.IsActive, action.SortOrder });
            });

            builder.Entity<AppPageAction>(entity =>
            {
                entity.ToTable("AppPageActions");
                entity.HasKey(link => new { link.AppPageId, link.PermissionActionId });
                entity.HasOne(link => link.AppPage)
                    .WithMany(page => page.Actions)
                    .HasForeignKey(link => link.AppPageId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(link => link.PermissionAction)
                    .WithMany(action => action.Pages)
                    .HasForeignKey(link => link.PermissionActionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureSubscriptionManagement(ModelBuilder builder)
        {
            builder.Entity<SubscriptionPlan>(entity =>
            {
                entity.ToTable("SubscriptionPlans", table =>
                {
                    table.HasCheckConstraint("CK_SubscriptionPlans_Price", "[Price] >= 0");
                    table.HasCheckConstraint("CK_SubscriptionPlans_Duration", "[DurationInDays] BETWEEN 1 AND 3650");
                });
                entity.HasKey(plan => plan.Id);
                entity.HasIndex(plan => plan.Code).IsUnique();
                entity.HasIndex(plan => new { plan.IsActive, plan.Name });
                entity.Property(plan => plan.Price).HasPrecision(18, 2);
                entity.Property(plan => plan.RowVersion).IsRowVersion();
            });

            builder.Entity<PlanPermission>(entity =>
            {
                entity.ToTable("PlanPermissions");
                entity.HasKey(permission => new
                {
                    permission.SubscriptionPlanId,
                    permission.AppPageId,
                    permission.PermissionActionId
                });
                entity.HasOne(permission => permission.SubscriptionPlan)
                    .WithMany(plan => plan.Permissions)
                    .HasForeignKey(permission => permission.SubscriptionPlanId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(permission => permission.AppPageAction)
                    .WithMany()
                    .HasForeignKey(permission => new
                    {
                        permission.AppPageId,
                        permission.PermissionActionId
                    })
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CompanySubscription>(entity =>
            {
                entity.ToTable("CompanySubscriptions", table =>
                {
                    table.HasCheckConstraint("CK_CompanySubscriptions_Dates", "[EndsAtUtc] > [StartsAtUtc]");
                    table.HasCheckConstraint("CK_CompanySubscriptions_Price", "[PriceSnapshot] >= 0");
                    table.HasCheckConstraint("CK_CompanySubscriptions_Duration", "[DurationInDaysSnapshot] BETWEEN 1 AND 3650");
                });
                entity.HasKey(subscription => subscription.Id);
                entity.HasAlternateKey(subscription => new { subscription.CompanyId, subscription.Id });
                entity.HasIndex(subscription => new { subscription.CompanyId, subscription.Status, subscription.EndsAtUtc });
                entity.HasIndex(subscription => subscription.CompanyId)
                    .IsUnique()
                    .HasFilter("[Status] IN (0, 1, 2)");
                entity.Property(subscription => subscription.PriceSnapshot).HasPrecision(18, 2);
                entity.Property(subscription => subscription.RowVersion).IsRowVersion();
                entity.HasOne(subscription => subscription.Company)
                    .WithMany()
                    .HasForeignKey(subscription => subscription.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(subscription => subscription.SubscriptionPlan)
                    .WithMany(plan => plan.CompanySubscriptions)
                    .HasForeignKey(subscription => subscription.SubscriptionPlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CompanyEntitlement>(entity =>
            {
                entity.ToTable("CompanyEntitlements");
                entity.HasKey(entitlement => new
                {
                    entitlement.CompanySubscriptionId,
                    entitlement.AppPageId,
                    entitlement.PermissionActionId
                });
                entity.HasIndex(entitlement => new
                {
                    entitlement.CompanyId,
                    entitlement.AppPageId,
                    entitlement.PermissionActionId
                });
                entity.HasOne(entitlement => entitlement.CompanySubscription)
                    .WithMany(subscription => subscription.Entitlements)
                    .HasForeignKey(entitlement => new
                    {
                        entitlement.CompanyId,
                        entitlement.CompanySubscriptionId
                    })
                    .HasPrincipalKey(subscription => new
                    {
                        subscription.CompanyId,
                        subscription.Id
                    })
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(entitlement => entitlement.AppPageAction)
                    .WithMany()
                    .HasForeignKey(entitlement => new
                    {
                        entitlement.AppPageId,
                        entitlement.PermissionActionId
                    })
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<SubscriptionChangeLog>(entity =>
            {
                entity.ToTable("SubscriptionChangeLogs");
                entity.HasKey(change => change.Id);
                entity.HasIndex(change => new
                {
                    change.CompanyId,
                    change.CompanySubscriptionId,
                    change.OccurredAtUtc
                });
                entity.HasOne(change => change.CompanySubscription)
                    .WithMany(subscription => subscription.ChangeLogs)
                    .HasForeignKey(change => new
                    {
                        change.CompanyId,
                        change.CompanySubscriptionId
                    })
                    .HasPrincipalKey(subscription => new
                    {
                        subscription.CompanyId,
                        subscription.Id
                    })
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureEffectivePermissions(ModelBuilder builder)
        {
            builder.Entity<UserPermission>(entity =>
            {
                entity.ToTable("UserPermissions");
                entity.HasKey(permission => permission.Id);
                entity.HasIndex(permission => new
                {
                    permission.CompanyId,
                    permission.UserId,
                    permission.AppPageId,
                    permission.PermissionActionId
                }).IsUnique();
                entity.HasIndex(permission => new { permission.UserId, permission.CompanyId });
                entity.HasOne(permission => permission.Company)
                    .WithMany()
                    .HasForeignKey(permission => permission.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(permission => permission.User)
                    .WithMany()
                    .HasForeignKey(permission => permission.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(permission => permission.AppPageAction)
                    .WithMany()
                    .HasForeignKey(permission => new
                    {
                        permission.AppPageId,
                        permission.PermissionActionId
                    })
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CompanyPermissionVersion>(entity =>
            {
                entity.ToTable("CompanyPermissionVersions", table =>
                    table.HasCheckConstraint("CK_CompanyPermissionVersions_Version", "[Version] > 0"));
                entity.HasKey(version => version.CompanyId);
                entity.HasOne(version => version.Company)
                    .WithOne()
                    .HasForeignKey<CompanyPermissionVersion>(version => version.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureAuthenticationSecurity(ModelBuilder builder)
        {
            builder.Entity<OtpChallenge>(entity =>
            {
                entity.HasIndex(challenge => new { challenge.UserId, challenge.CreatedAtUtc });
                entity.Property(challenge => challenge.OtpHash).HasMaxLength(64);
            });

            builder.Entity<UserSession>(entity =>
            {
                entity.ToTable("UserSessions");
                entity.HasKey(session => session.Id);
                entity.HasIndex(session => session.RefreshTokenHash).IsUnique();
                entity.HasIndex(session => new
                {
                    session.UserId,
                    session.RevokedAtUtc,
                    session.ExpiresAtUtc
                });
                entity.Property(session => session.SecurityStamp).HasMaxLength(450);
                entity.Property(session => session.RowVersion).IsRowVersion();
                entity.HasOne(session => session.User)
                    .WithMany()
                    .HasForeignKey(session => session.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AppLog>(entity =>
            {
                entity.HasIndex(log => log.CreatedAtUtc);
                entity.HasIndex(log => new { log.CompanyId, log.OccurredAtUtc });
                entity.HasIndex(log => log.ReferenceNumber).IsUnique();
                entity.Property(log => log.Message).HasMaxLength(2000);
                entity.Property(log => log.Route).HasMaxLength(500);
                entity.Property(log => log.Action).HasMaxLength(200);
                entity.Property(log => log.FunctionName).HasMaxLength(500);
                entity.Property(log => log.SourceFile).HasMaxLength(260);
                entity.Property(log => log.RequestMethod).HasMaxLength(16);
                entity.Property(log => log.CorrelationId).HasMaxLength(100);
                entity.Property(log => log.ReferenceNumber).HasMaxLength(64);
                entity.Property(log => log.CompanyName).HasMaxLength(100);
                entity.Property(log => log.UserId).HasMaxLength(450);
                entity.Property(log => log.UserName).HasMaxLength(256);
                entity.Property(log => log.ExceptionType).HasMaxLength(300);
                entity.Property(log => log.StackTrace).HasMaxLength(8000);
                entity.Property(log => log.Hash).HasMaxLength(128);
                entity.Property(log => log.Platform).HasMaxLength(100);
                entity.Property(log => log.DeviceModel).HasMaxLength(300);
                entity.Property(log => log.AppVersion).HasMaxLength(100);
                entity.Property(log => log.MetadataJson).HasMaxLength(4000);
            });
        }
    }
}
