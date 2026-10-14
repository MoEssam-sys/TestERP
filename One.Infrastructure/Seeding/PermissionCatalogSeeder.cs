using Microsoft.EntityFrameworkCore;
using One.Domain.Constants;
using One.Domain.Entities.Identity;
using One.Infrastructure.DbContext.UsersDbContext;

namespace One.Infrastructure.Seeding
{
    public static class PermissionCatalogSeeder
    {
        public static async Task SeedAsync(
            UsersDbContext context,
            CancellationToken cancellationToken = default)
        {
            var modules = await context.AppModules
                .ToDictionaryAsync(module => module.Key, StringComparer.Ordinal, cancellationToken);

            foreach (var definition in ModuleDefinitions)
            {
                if (!modules.TryGetValue(definition.Key, out var module))
                {
                    module = new AppModule { Key = definition.Key };
                    modules.Add(definition.Key, module);
                    context.AppModules.Add(module);
                }

                module.NameResourceKey = definition.NameResourceKey;
                module.SortOrder = definition.SortOrder;
                module.IsActive = true;
                module.IsAssignableToPlans = definition.IsAssignableToPlans;
            }

            var actions = await context.PermissionActions
                .ToDictionaryAsync(action => action.Key, StringComparer.Ordinal, cancellationToken);

            foreach (var definition in ActionDefinitions)
            {
                if (!actions.TryGetValue(definition.Key, out var action))
                {
                    action = new PermissionAction { Key = definition.Key };
                    actions.Add(definition.Key, action);
                    context.PermissionActions.Add(action);
                }

                action.NameResourceKey = definition.NameResourceKey;
                action.SortOrder = definition.SortOrder;
                action.IsActive = true;
            }

            await context.SaveChangesAsync(cancellationToken);

            var pages = await context.AppPages
                .ToDictionaryAsync(page => page.Key, StringComparer.Ordinal, cancellationToken);

            foreach (var definition in PageDefinitions)
            {
                if (!pages.TryGetValue(definition.Key, out var page))
                {
                    page = new AppPage { Key = definition.Key };
                    pages.Add(definition.Key, page);
                    context.AppPages.Add(page);
                }

                page.AppModuleId = modules[definition.ModuleKey].Id;
                page.NameResourceKey = definition.NameResourceKey;
                page.Route = definition.Route;
                page.SortOrder = definition.SortOrder;
                page.IsNavigationVisible = definition.IsNavigationVisible;
                page.IsActive = true;
            }

            await context.SaveChangesAsync(cancellationToken);

            var seededPageIds = PageDefinitions
                .Select(definition => pages[definition.Key].Id)
                .ToArray();
            var existingLinks = await context.AppPageActions
                .Where(link => seededPageIds.Contains(link.AppPageId))
                .ToListAsync(cancellationToken);
            var existingKeys = existingLinks
                .Select(link => (link.AppPageId, link.PermissionActionId))
                .ToHashSet();
            var desiredKeys = PageDefinitions
                .SelectMany(
                    page => page.ActionKeys,
                    (page, actionKey) => (
                        AppPageId: pages[page.Key].Id,
                        PermissionActionId: actions[actionKey].Id))
                .ToHashSet();

            context.AppPageActions.RemoveRange(existingLinks.Where(link =>
                !desiredKeys.Contains((link.AppPageId, link.PermissionActionId))));

            foreach (var key in desiredKeys.Where(key => !existingKeys.Contains(key)))
            {
                context.AppPageActions.Add(new AppPageAction
                {
                    AppPageId = key.AppPageId,
                    PermissionActionId = key.PermissionActionId
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static readonly CatalogModuleDefinition[] ModuleDefinitions =
        [
            new(PermissionCatalogKeys.Modules.SystemAdministration, "CatalogModuleSystemAdministration", 10, false),
            new(PermissionCatalogKeys.Modules.CompanyAdministration, "CatalogModuleCompanyAdministration", 20, true),
            new(PermissionCatalogKeys.Modules.General, "CatalogModuleGeneral", 30, true),
            new(PermissionCatalogKeys.Modules.Sales, "CatalogModuleSales", 40, true),
            new(PermissionCatalogKeys.Modules.Inventory, "CatalogModuleInventory", 50, true)
        ];

        private static readonly CatalogActionDefinition[] ActionDefinitions =
        [
            new(PermissionCatalogKeys.Actions.View, "CatalogPermissionView", 10),
            new(PermissionCatalogKeys.Actions.Create, "CatalogPermissionCreate", 20),
            new(PermissionCatalogKeys.Actions.Update, "CatalogPermissionUpdate", 30),
            new(PermissionCatalogKeys.Actions.Delete, "CatalogPermissionDelete", 40),
            new(PermissionCatalogKeys.Actions.Activate, "CatalogPermissionActivate", 50),
            new(PermissionCatalogKeys.Actions.Export, "CatalogPermissionExport", 60),
            new(PermissionCatalogKeys.Actions.Approve, "CatalogPermissionApprove", 70)
        ];

        private static readonly CatalogPageDefinition[] PageDefinitions =
        [
            new(PermissionCatalogKeys.Pages.SystemDashboard, PermissionCatalogKeys.Modules.SystemAdministration, "Dashboard", "/superadmin/dashboard", 10, true, [PermissionCatalogKeys.Actions.View]),
            new(PermissionCatalogKeys.Pages.Restaurants, PermissionCatalogKeys.Modules.SystemAdministration, "RestaurantsManagement", "/sysadmin/restaurants", 20, true, CrudWithActivation),
            new(PermissionCatalogKeys.Pages.CompanyDetails, PermissionCatalogKeys.Modules.SystemAdministration, "RestaurantDetails", "/superadmin/restaurants/{CompanyId:int}", 30, false, [PermissionCatalogKeys.Actions.View, PermissionCatalogKeys.Actions.Update, PermissionCatalogKeys.Actions.Activate]),
            new(PermissionCatalogKeys.Pages.ErrorLogs, PermissionCatalogKeys.Modules.SystemAdministration, "ErrorLogs", "/sysadmin/error-logs", 40, true, [PermissionCatalogKeys.Actions.View]),
            new(PermissionCatalogKeys.Pages.SystemHealth, PermissionCatalogKeys.Modules.SystemAdministration, "SystemHealth", "/sysadmin/health", 50, true, [PermissionCatalogKeys.Actions.View]),
            new(PermissionCatalogKeys.Pages.Users, PermissionCatalogKeys.Modules.CompanyAdministration, "ManageUsers", "/users-management", 10, true, [PermissionCatalogKeys.Actions.View, PermissionCatalogKeys.Actions.Create, PermissionCatalogKeys.Actions.Update, PermissionCatalogKeys.Actions.Activate]),
            new(PermissionCatalogKeys.Pages.RegisterUser, PermissionCatalogKeys.Modules.CompanyAdministration, "RegisterUser", "/register-user", 20, true, [PermissionCatalogKeys.Actions.Create]),
            new(PermissionCatalogKeys.Pages.RegisterAdmin, PermissionCatalogKeys.Modules.SystemAdministration, "CatalogPageRegisterAdmin", "/register-admin/{CompanyId:int}", 60, false, [PermissionCatalogKeys.Actions.Create]),
            new(PermissionCatalogKeys.Pages.SubscriptionPlans, PermissionCatalogKeys.Modules.SystemAdministration, "SubscriptionPlans", "/sysadmin/subscription-plans", 70, true, CrudWithActivation),
            new(PermissionCatalogKeys.Pages.GeneralDashboard, PermissionCatalogKeys.Modules.General, "Dashboard", "/", 10, true, [PermissionCatalogKeys.Actions.View]),
            new(PermissionCatalogKeys.Pages.SalesDashboard, PermissionCatalogKeys.Modules.Sales, "SalesDashboard", "/sales", 10, true, [PermissionCatalogKeys.Actions.View, PermissionCatalogKeys.Actions.Export]),
            new(PermissionCatalogKeys.Pages.SalesInvoices, PermissionCatalogKeys.Modules.Sales, "Invoices", "/sales/invoices", 20, false, TransactionActions),
            new(PermissionCatalogKeys.Pages.SalesOrders, PermissionCatalogKeys.Modules.Sales, "Orders", "/sales/orders", 30, false, TransactionActions),
            new(PermissionCatalogKeys.Pages.SalesBrands, PermissionCatalogKeys.Modules.Sales, "Brands", "/sales/brands", 40, true, CrudWithActivationAndExport),
            new(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Modules.Sales, "Branches", "/sales/branches", 50, true, CrudWithActivationAndExport),
            new(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Modules.Inventory, "Warehouses", "/stocks/warehouses", 10, true, CrudWithActivationAndExport)
        ];

        private static string[] CrudWithActivation =>
        [
            PermissionCatalogKeys.Actions.View,
            PermissionCatalogKeys.Actions.Create,
            PermissionCatalogKeys.Actions.Update,
            PermissionCatalogKeys.Actions.Delete,
            PermissionCatalogKeys.Actions.Activate
        ];

        private static string[] CrudWithActivationAndExport =>
        [
            .. CrudWithActivation,
            PermissionCatalogKeys.Actions.Export
        ];

        private static string[] TransactionActions =>
        [
            PermissionCatalogKeys.Actions.View,
            PermissionCatalogKeys.Actions.Create,
            PermissionCatalogKeys.Actions.Update,
            PermissionCatalogKeys.Actions.Delete,
            PermissionCatalogKeys.Actions.Export,
            PermissionCatalogKeys.Actions.Approve
        ];

        private sealed record CatalogModuleDefinition(
            string Key,
            string NameResourceKey,
            int SortOrder,
            bool IsAssignableToPlans);

        private sealed record CatalogActionDefinition(
            string Key,
            string NameResourceKey,
            int SortOrder);

        private sealed record CatalogPageDefinition(
            string Key,
            string ModuleKey,
            string NameResourceKey,
            string Route,
            int SortOrder,
            bool IsNavigationVisible,
            IReadOnlyList<string> ActionKeys);
    }
}
