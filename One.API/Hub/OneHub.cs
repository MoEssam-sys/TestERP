using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using One.Domain.Constants;
using One.Infrastructure.Interfaces;

namespace One.API.Hub
{
    [Authorize]
    public sealed class OneHub(IPermissionService permissionService) : Microsoft.AspNetCore.SignalR.Hub
    {
        public override async Task OnConnectedAsync()
        {
            var companyId = GetCompanyId();
            if (!companyId.HasValue)
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, CompanyGroup(companyId.Value));
            await base.OnConnectedAsync();
        }

        public async Task SendMessage(string message)
        {
            var companyId = GetCompanyId();
            if (!companyId.HasValue ||
                !await permissionService.HasPermissionAsync(
                    Context.User!,
                    PermissionCatalogKeys.Pages.GeneralDashboard,
                    PermissionCatalogKeys.Actions.View,
                    Context.ConnectionAborted))
            {
                throw new HubException("Permission denied.");
            }

            var sender = Context.UserIdentifier ?? "unknown";
            await Clients.Group(CompanyGroup(companyId.Value))
                .SendAsync("ReceiveMessage", sender, message, Context.ConnectionAborted);
        }

        private int? GetCompanyId() =>
            int.TryParse(Context.User?.FindFirst("companyId")?.Value, out var companyId)
                ? companyId
                : null;

        private static string CompanyGroup(int companyId) => $"company:{companyId}";
    }
}
