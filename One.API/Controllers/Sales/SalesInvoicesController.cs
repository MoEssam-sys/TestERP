using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.API.Authorization;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs.Sales;
using One.Infrastructure.Interfaces;

namespace One.API.Controllers.Sales
{
    [Authorize]
    [ApiController]
    [Route("api/sales/invoices")]
    public sealed class SalesInvoicesController(
        ISalesInvoiceApplicationService invoiceService) : BaseApiController
    {
        [HttpPost]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesInvoices, PermissionCatalogKeys.Actions.Create)]
        public async Task<ActionResult<Result<SalesInvoiceCreatedDto>>> Create(
            CreateSalesInvoiceCommand? command,
            CancellationToken cancellationToken)
        {
            var userId = GetAuthenticatedUserId();
            if (userId is null)
                return Unauthorized(Result<SalesInvoiceCreatedDto>.Fail(["User not authenticated."], 401));

            var result = await invoiceService.CreateAsync(
                command,
                userId.Value,
                cancellationToken);
            return ToActionResult(
                result,
                statusOverride: result.Success ? StatusCodes.Status201Created : null);
        }
    }
}
