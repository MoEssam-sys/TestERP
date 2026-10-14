using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.API.Authorization;
using One.Application.Repos.Stocks;
using One.Application.Services.Mapping;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs;
using One.Domain.DTOs.Stocks;
using One.Domain.Entities.Stocks;
using One.Domain.Enums;

namespace One.API.Controllers.Stockes
{
    [Authorize]
    [ApiController]
    [Route("api/stocks/[controller]")]
    public class WarehousesController : BaseApiController
    {
        private readonly WarehousesRepo _repo;

        private readonly IDynamicMappingService _mapper;

        private readonly ILogger<WarehousesController> _logger;

        public WarehousesController(WarehousesRepo repo, IDynamicMappingService mapper, ILogger<WarehousesController> logger)
        {
            _repo = repo;
            _mapper = mapper;
            _logger = logger;
        }

        // GET: api/sales/Warehouses
        [HttpGet]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<List<Warehouses>>>> GetAll()
        {
            var res = await _repo.GetAllAsync(HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        [HttpPost("filter")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<PagedResult<Warehouses>>>> Filter([FromBody] FilterWarehouseRequest filter)
        {
            if (!ModelState.IsValid)
                return ToPagedActionResult(Result<PagedResult<Warehouses>>.Fail(new() { "Validation failed." }, 400));

            var res = await _repo.QueryByFilterAsync(filter, HttpContext.RequestAborted);
            return ToPagedActionResult(res);
        }

        // GET: api/sales/Warehouses/{id}
        [HttpGet("{id:guid}")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<Warehouses?>>> GetById(Guid id)
        {
            var res = await _repo.GetByIdAsync(id, HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // POST: api/sales/Warehouses/create
        [HttpPost("create")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.Create)]
        public async Task<ActionResult<Result<Warehouses>>> Create([FromBody] CreateWarehouseRequest dto)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<Warehouses>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(Result<Warehouses>.Fail(new() { "User not authenticated." }, 401));

            // بنستخدم المابر الدايناميك → هيملأ الخصائص اللي عليها [InsertField]
            var entity = _mapper.MapCreate<Warehouses, CreateWarehouseRequest>(dto);

            var insertRes = await _repo.InsertAndReturnAsync(entity, userId.Value, cancellationToken: HttpContext.RequestAborted);
            if (!insertRes.Success)
                return ToActionResult(Result<Warehouses>.Fail(insertRes.Messages ?? new() { "Insert failed." },
                                                          insertRes.StatusCode ?? 400));

            return ToActionResult(insertRes, statusOverride: 201);
        }

        // PUT: api/sales/Warehouses/update
        [HttpPut("update")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.Update)]
        public async Task<ActionResult<Result<bool>>> Update([FromBody] UpdateWarehouseRequest dto)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(Result<bool>.Fail(new() { "User not authenticated." }, 401));

            // المابر الدايناميك هيملأ [UpdateField] + Id
            var entity = _mapper.MapUpdate<Warehouses, UpdateWarehouseRequest>(dto);

            var res = await _repo.UpdateAsync(entity, userId.Value, cancellationToken: HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // DELETE: api/sales/Warehouses/delete
        [HttpDelete("delete")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.Delete)]
        public async Task<ActionResult<Result<bool>>> Delete(
            [FromBody] DeleteEntityRequest<Guid> request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId is null)
                return Unauthorized(Result<bool>.Fail(["User not authenticated."], 401));

            var res = await _repo.DeleteAsync(request, userId.Value, cancellationToken);
            return ToActionResult(res);
        }

        // PUT: api/sales/Warehouses/active
        [HttpPut("active")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.Activate)]
        public async Task<ActionResult<Result<bool>>> SetActive([FromBody] ActiveWarehouseRequest dto)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(Result<bool>.Fail(new() { "User not authenticated." }, 401));

            // المابر ينسخ الـ Id (وأي مفاتيح ActiveField أخرى لو موجودة) + نمرر IsActive من الـ DTO
            var keyEntity = _mapper.MapActive<Warehouses, ActiveWarehouseRequest>(dto);

            var res = await _repo.SetActiveAsync(keyEntity, dto.IsActive, userId.Value, HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // GET: api/sales/Warehousescrud/firstOrDefault
        [HttpGet("firstOrDefault")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<Warehouses?>>> FirstOrDefaultAsync(Guid id)
        {
            var res = await _repo.FirstOrDefaultAsync("Id = @id", new { id }, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/Warehousescrud/exists/code/{code}
        [HttpGet("exists/code/{code}")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<bool>>> IsCodeUnique(string code)
        {
            var res = await _repo.IsUniqueAsync(nameof(Warehouses.Code), code, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // POST: api/sales/Warehousescrud/search
        [HttpPost("search")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<List<Warehouses>>>> Search([FromBody] SearchRequest req)
        {
            var res = await _repo.SearchAsync(
                columns: new[] { nameof(Warehouses.Name), nameof(Warehouses.Code), nameof(Warehouses.BranchId) },
                query: req.Query,
                maxResults: req.MaxResults ?? 10,
                likeCond: LikeConditions.OR,
                isActive: req.IsActive,                    // null/true/false حسب ما تبعت
                firstId: req.FirstId,
                firstIdColumn: req.FirstIdColumn,          // مثال: "CompanyId"
                secondId: req.SecondId,
                secondIdColumn: req.SecondIdColumn,        // مثال: "BranchId"
                cancellationToken: HttpContext.RequestAborted
            );

            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // POST: api/sales/Warehousescrud/ids
        [HttpPost("ids")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<List<Warehouses>>>> GetByIds([FromBody] IEnumerable<Guid> ids)
        {
            var res = await _repo.GetByIdsAsync(ids, cancellationToken: HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/Warehousescrud/last
        [HttpGet("last")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<Warehouses?>>> GetLast()
        {
            // تقدر تغيّر العمود حسب ترتيبك: CreatedAt/Id/Code...
            var res = await _repo.GetLastAsync(orderByColumn: nameof(Warehouses.CreatedAt), cancellationToken: HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/Warehousescrud/random
        [HttpGet("random")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<Warehouses?>>> GetRandom()
        {
            var res = await _repo.GetRandomAsync(HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // PUT: api/sales/Warehousescrud/active
        // Body: { "id": "GUID", "isActive": true }
        [HttpPut("activeDapper")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.Activate)]
        public async Task<ActionResult<Result<bool>>> SetActiveByColumn([FromBody] ActiveWarehouseRequest dto)
        {
            if (!ModelState.IsValid)
                return StatusCode(400, Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId is null)
                return Unauthorized(Result<bool>.Fail(["User not authenticated."], 401));

            var entity = _mapper.MapActive<Warehouses, ActiveWarehouseRequest>(dto);
            var res = await _repo.SetActiveAsync(entity, dto.IsActive, userId.Value, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // أمثلة سريعة إضافية

        // GET: api/sales/Warehousescrud/find?minCreated=2025-01-01
        [HttpGet("find")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<List<Warehouses>>>> Find([FromQuery] DateTime? minCreated = null)
        {
            if (minCreated == null)
                return StatusCode(400, Result<List<Warehouses>>.Fail(new() { "minCreated is required." }, 400));

            var res = await _repo.FindAsync("CreatedAt >= @d", new { d = minCreated }, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/Warehousescrud/count?active=true
        [HttpGet("count")]
        [RequirePermission(PermissionCatalogKeys.Pages.InventoryWarehouses, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<int>>> Count([FromQuery] bool? active = null)
        {
            Result<int> res;
            if (active.HasValue)
                res = await _repo.CountAsync("IsActive = @x", new { x = active.Value }, HttpContext.RequestAborted);
            else
                res = await _repo.CountAsync(cancellationToken: HttpContext.RequestAborted);

            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }



    }
}
