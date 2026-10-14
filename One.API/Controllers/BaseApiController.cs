using Microsoft.AspNetCore.Mvc;
using One.Domain.Base;
using System.Security.Claims;

namespace One.API.Controllers
{
    /// <summary>
    /// Base controller providing shared helpers for all API controllers.
    /// Eliminates duplicated GetAuthenticatedUserId / ToActionResult / ToPagedActionResult across every controller.
    /// </summary>
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        // ========= Auth Helpers =========

        protected Guid? GetAuthenticatedUserId()
        {
            var str =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("uid");

            return Guid.TryParse(str, out var g) ? g : null;
        }

        // ========= Result Helpers =========

        protected ActionResult<Result<T>> ToActionResult<T>(Result<T> res, int? statusOverride = null)
        {
            var status = statusOverride ?? res.StatusCode ?? (res.Success ? 200 : 400);
            return StatusCode(status, res);
        }

        protected ActionResult<Result<PagedResult<TItem>>> ToPagedActionResult<TItem>(
            Result<PagedResult<TItem>> res,
            int? statusOverride = null,
            bool writePaginationHeaders = true)
        {
            var status = statusOverride ?? res.StatusCode ?? (res.Success ? 200 : 400);

            if (!res.Success)
                return StatusCode(status, res);

            if (writePaginationHeaders && res.Data is { } pg)
            {
                Response.Headers["X-Total-Count"] = pg.TotalCount.ToString();
                Response.Headers["X-Total-Pages"] = pg.TotalPages.ToString();
                Response.Headers["X-Page-Number"] = pg.PageNumber.ToString();
                Response.Headers["X-Page-Size"] = pg.PageSize.ToString();
                Response.Headers["X-Has-Next"] = pg.HasNextPage.ToString().ToLowerInvariant();
                Response.Headers["X-Has-Prev"] = pg.HasPreviousPage.ToString().ToLowerInvariant();
            }

            return StatusCode(status, res);
        }
    }
}
