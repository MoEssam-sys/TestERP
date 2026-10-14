using One.Domain.Enums;

namespace One.Domain.DTOs.Identity
{
    public sealed record PermissionSelectionDto(string PageKey, string ActionKey);

    public sealed record CreateSubscriptionPlanRequest(
        string Code,
        string Name,
        string? Description,
        decimal Price,
        string Currency,
        int DurationInDays,
        IReadOnlyList<PermissionSelectionDto> Permissions);

    public sealed record UpdateSubscriptionPlanRequest(
        string Code,
        string Name,
        string? Description,
        decimal Price,
        string Currency,
        int DurationInDays,
        IReadOnlyList<PermissionSelectionDto> Permissions,
        string RowVersion);

    public sealed record SetSubscriptionPlanActiveRequest(bool IsActive, string RowVersion);

    public sealed record SubscriptionPlanDto(
        int Id,
        string Code,
        string Name,
        string? Description,
        decimal Price,
        string Currency,
        int DurationInDays,
        bool IsActive,
        string RowVersion,
        IReadOnlyList<PermissionSelectionDto> Permissions);

    public sealed record CreateCompanySubscriptionRequest(
        int CompanyId,
        int SubscriptionPlanId,
        DateTime? StartsAtUtc,
        IReadOnlyList<PermissionSelectionDto>? Entitlements);

    public sealed record RenewCompanySubscriptionRequest(
        int? SubscriptionPlanId,
        IReadOnlyList<PermissionSelectionDto>? Entitlements,
        string RowVersion);

    public sealed record ReplaceCompanyEntitlementsRequest(
        IReadOnlyList<PermissionSelectionDto> Entitlements,
        string RowVersion);

    public sealed record ChangeSubscriptionStatusRequest(string RowVersion);

    public sealed record SubscriptionChangeLogDto(
        long Id,
        SubscriptionChangeType ChangeType,
        SubscriptionStatus? PreviousStatus,
        SubscriptionStatus NewStatus,
        string? Details,
        DateTime OccurredAtUtc,
        string? PerformedByUserId);

    public sealed record CompanySubscriptionDto(
        long Id,
        int CompanyId,
        int SubscriptionPlanId,
        string PlanCode,
        string PlanName,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        SubscriptionStatus Status,
        decimal PriceSnapshot,
        string CurrencySnapshot,
        int DurationInDaysSnapshot,
        bool IsCustomized,
        string RowVersion,
        IReadOnlyList<PermissionSelectionDto> Entitlements,
        IReadOnlyList<SubscriptionChangeLogDto> ChangeLogs);
}
