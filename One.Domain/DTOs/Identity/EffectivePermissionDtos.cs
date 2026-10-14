using One.Domain.Enums;

namespace One.Domain.DTOs.Identity
{
    public sealed record NavigationPageDto(
        string Key,
        string NameResourceKey,
        string Route,
        int SortOrder);

    public sealed record NavigationModuleDto(
        string Key,
        string NameResourceKey,
        int SortOrder,
        IReadOnlyList<NavigationPageDto> Pages);

    public sealed record EffectivePermissionsDto(
        long Version,
        bool IsSuperAdmin,
        int? CompanyId,
        SubscriptionStatus? SubscriptionStatus,
        DateTime? SubscriptionEndsAtUtc,
        IReadOnlyList<PermissionSelectionDto> Permissions,
        IReadOnlyList<NavigationModuleDto> Navigation);

    public sealed record UserPermissionEditorDto(
        string UserId,
        string UserName,
        int CompanyId,
        long Version,
        IReadOnlyList<PermissionCatalogModuleDto> AvailablePermissions,
        IReadOnlyList<PermissionSelectionDto> GrantedPermissions);

    public sealed record ReplaceUserPermissionsRequest(
        IReadOnlyList<PermissionSelectionDto> Permissions,
        long Version);
}
