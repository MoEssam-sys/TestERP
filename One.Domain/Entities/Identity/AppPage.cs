using System.ComponentModel.DataAnnotations;

namespace One.Domain.Entities.Identity
{
    public sealed class AppPage
    {
        public int Id { get; set; }

        public int AppModuleId { get; set; }

        [Required, MaxLength(150)]
        public string Key { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string NameResourceKey { get; set; } = string.Empty;

        [Required, MaxLength(250)]
        public string Route { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public bool IsNavigationVisible { get; set; }

        public bool IsActive { get; set; } = true;

        public AppModule AppModule { get; set; } = null!;

        public ICollection<AppPageAction> Actions { get; set; } = [];
    }
}
