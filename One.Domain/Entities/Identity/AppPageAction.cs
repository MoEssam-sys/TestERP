namespace One.Domain.Entities.Identity
{
    public sealed class AppPageAction
    {
        public int AppPageId { get; set; }

        public int PermissionActionId { get; set; }

        public AppPage AppPage { get; set; } = null!;

        public PermissionAction PermissionAction { get; set; } = null!;
    }
}
