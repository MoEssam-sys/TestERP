namespace One.Domain.Constants
{
    public static class PermissionCatalogKeys
    {
        public static class Modules
        {
            public const string SystemAdministration = "SystemAdministration";
            public const string CompanyAdministration = "CompanyAdministration";
            public const string General = "General";
            public const string Sales = "Sales";
            public const string Inventory = "Inventory";
        }

        public static class Pages
        {
            public const string SystemDashboard = "SystemAdministration.Dashboard";
            public const string Companies = "SystemAdministration.Companies";
            public const string Restaurants = Companies;
            public const string CompanyDetails = "SystemAdministration.CompanyDetails";
            public const string ErrorLogs = "SystemAdministration.ErrorLogs";
            public const string SystemHealth = "SystemAdministration.SystemHealth";
            public const string Users = "SystemAdministration.Users";
            public const string RegisterUser = "SystemAdministration.RegisterUser";
            public const string RegisterAdmin = "SystemAdministration.RegisterAdmin";
            public const string SubscriptionPlans = "SystemAdministration.SubscriptionPlans";
            public const string GeneralDashboard = "General.Dashboard";
            public const string SalesDashboard = "Sales.Dashboard";
            public const string SalesInvoices = "Sales.Invoices";
            public const string SalesOrders = "Sales.Orders";
            public const string SalesBrands = "Sales.Brands";
            public const string SalesBranches = "Sales.Branches";
            public const string InventoryWarehouses = "Inventory.Warehouses";
        }

        public static class Actions
        {
            public const string View = "View";
            public const string Create = "Create";
            public const string Update = "Update";
            public const string Delete = "Delete";
            public const string Activate = "Activate";
            public const string Export = "Export";
            public const string Approve = "Approve";
        }
    }
}
