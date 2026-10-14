using System;

namespace One.Infrastructure.Constants
{
    public static class SeedConstants
    {
        // Role IDs
        public const string SuperAdminRoleId = "11111111-1111-1111-1111-111111111111";
        public const string CompanyAdminRoleId = "22222222-2222-2222-2222-222222222222";
        public const string UserRoleId = "33333333-3333-3333-3333-333333333333";
        public const string TesterRoleId = "44444444-4444-4444-4444-444444444444";

        // User IDs
        public const string SuperAdminUserId = "99999999-9999-9999-9999-999999999999";

        // Stamps (Static to prevent PendingModelChangesWarning)
        public const string SuperAdminSecurityStamp = "55555555-5555-5555-5555-555555555555";
        public const string SuperAdminConcurrencyStamp = "66666666-6666-6666-6666-666666666666";

        // Role Concurrency Stamps
        public const string SuperAdminRoleConcurrencyStamp = "77777777-7777-7777-7777-777777777777";
        public const string CompanyAdminRoleConcurrencyStamp = "88888888-8888-8888-8888-888888888888";
        public const string UserRoleConcurrencyStamp = "99999999-9999-ABCD-ABCD-999999999999";
        public const string TesterRoleConcurrencyStamp = "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA";
    }
}
