namespace One.Domain.Constants
{
    public static class SystemRoles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string CompanyAdmin = "CompanyAdmin";
        public const string User = "User";
        public const string Tester = "Tester";
    }

    public static class AuthorizationPolicies
    {
        public const string ManageUsers = "ManageUsers";
        public const string ManageCompanies = "ManageCompanies";
    }
}
