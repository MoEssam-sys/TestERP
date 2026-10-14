using Microsoft.AspNetCore.Identity;

namespace One.Domain.Entities.Identity
{
    public class UserData : IdentityUser
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public int Age { get; set; }

        // Multitenancy
        public int? CompanyId { get; set; }
        public virtual Company? Company { get; set; }

        public bool IsActive { get; set; } = true;
    }
}