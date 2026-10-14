namespace One.Domain.DTOs.Identity
{
    public sealed record PermissionCatalogActionDto(
        string Key,
        string NameResourceKey,
        int SortOrder);

    public sealed record PermissionCatalogPageDto(
        string Key,
        string NameResourceKey,
        string Route,
        int SortOrder,
        bool IsNavigationVisible,
        IReadOnlyList<PermissionCatalogActionDto> Actions);

    public sealed record PermissionCatalogModuleDto(
        string Key,
        string NameResourceKey,
        int SortOrder,
        bool IsAssignableToPlans,
        IReadOnlyList<PermissionCatalogPageDto> Pages);
}
