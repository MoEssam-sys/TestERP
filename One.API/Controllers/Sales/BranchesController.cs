using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.API.Authorization;
using One.Application.Repos.Sales;
using One.Application.Services.Mapping;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs;
using One.Domain.DTOs.Sales;
using One.Domain.Entities.Sales;
using One.Domain.Enums;

namespace One.API.Controllers.Sales
{
    [Authorize]
    [Route("api/sales/[controller]")]
    [ApiController]
    public class BranchesController : BaseApiController
    {
        private readonly BranchesRepo _repo;

        private readonly IDynamicMappingService _mapper;

        private readonly ILogger<BranchesController> _logger;

        public BranchesController(BranchesRepo repo, IDynamicMappingService mapper, ILogger<BranchesController> logger)
        {
            _repo = repo;
            _mapper = mapper;
            _logger = logger;
        }

        // GET: api/sales/Branches
        [HttpGet]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<List<Branches>>>> GetAll()
        {
            var res = await _repo.GetAllAsync(HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        [HttpPost("filter")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<PagedResult<Branches>>>> Filter([FromBody] FilterBranchRequest filter)
        {
            if (!ModelState.IsValid)
                return ToPagedActionResult(Result<PagedResult<Branches>>.Fail(new() { "Validation failed." }, 400));

            var res = await _repo.QueryByFilterAsync(filter, HttpContext.RequestAborted);
            return ToPagedActionResult(res);
        }

        // GET: api/sales/Branches/{id}
        [HttpGet("{id:guid}")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<Branches?>>> GetById(Guid id)
        {
            var res = await _repo.GetByIdAsync(id, HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // POST: api/sales/Branches/create
        [HttpPost("create")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.Create)]
        public async Task<ActionResult<Result<Branches>>> Create([FromBody] CreateBranchRequest dto)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<Branches>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(Result<Branches>.Fail(new() { "User not authenticated." }, 401));

            // بنستخدم المابر الدايناميك → هيملأ الخصائص اللي عليها [InsertField]
            var entity = _mapper.MapCreate<Branches, CreateBranchRequest>(dto);

            var insertRes = await _repo.InsertAndReturnAsync(entity, userId.Value, cancellationToken: HttpContext.RequestAborted);
            if (!insertRes.Success)
                return ToActionResult(Result<Branches>.Fail(insertRes.Messages ?? new() { "Insert failed." },
                                                          insertRes.StatusCode ?? 400));

            return ToActionResult(insertRes, statusOverride: 201);
        }

        // PUT: api/sales/Branches/update
        [HttpPut("update")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.Update)]
        public async Task<ActionResult<Result<bool>>> Update([FromBody] UpdateBranchRequest dto)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(Result<bool>.Fail(new() { "User not authenticated." }, 401));

            // المابر الدايناميك هيملأ [UpdateField] + Id
            var entity = _mapper.MapUpdate<Branches, UpdateBranchRequest>(dto);

            var res = await _repo.UpdateAsync(entity, userId.Value, cancellationToken: HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // DELETE: api/sales/Branches/delete
        [HttpDelete("delete")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.Delete)]
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

        // PUT: api/sales/Branches/active
        [HttpPut("active")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.Activate)]
        public async Task<ActionResult<Result<bool>>> SetActive([FromBody] ActiveBranchRequest dto)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(Result<bool>.Fail(new() { "User not authenticated." }, 401));

            // المابر ينسخ الـ Id (وأي مفاتيح ActiveField أخرى لو موجودة) + نمرر IsActive من الـ DTO
            var keyEntity = _mapper.MapActive<Branches, ActiveBranchRequest>(dto);

            var res = await _repo.SetActiveAsync(keyEntity, dto.IsActive, userId.Value, HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // GET: api/sales/Branchescrud/firstOrDefault
        [HttpGet("firstOrDefault")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<Branches?>>> FirstOrDefaultAsync(Guid id)
        {
            var res = await _repo.FirstOrDefaultAsync("Id = @id", new { id }, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/Branchescrud/exists/code/{code}
        [HttpGet("exists/code/{code}")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<bool>>> IsCodeUnique(string code)
        {
            var res = await _repo.IsUniqueAsync(nameof(Branches.Code), code, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // POST: api/sales/Branchescrud/search
        [HttpPost("search")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<List<Branches>>>> Search([FromBody] SearchRequest req)
        {
            var res = await _repo.SearchAsync(
                columns: [nameof(Branches.Name), nameof(Branches.Code), nameof(Branches.Phone)],
                query: req.Query,
                maxResults: req.MaxResults ?? 10,
                likeCond: LikeConditions.OR,
                isActive: req.IsActive,
                firstId: req.FirstId,
                firstIdColumn: req.FirstIdColumn,
                secondId: req.SecondId,
                secondIdColumn: req.SecondIdColumn,
                cancellationToken: HttpContext.RequestAborted
            );

            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // POST: api/sales/Branchescrud/ids
        [HttpPost("ids")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<List<Branches>>>> GetByIds([FromBody] IEnumerable<Guid> ids)
        {
            var res = await _repo.GetByIdsAsync(ids, cancellationToken: HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/Branchescrud/last
        [HttpGet("last")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<Branches?>>> GetLast()
        {
            // تقدر تغيّر العمود حسب ترتيبك: CreatedAt/Id/Code...
            var res = await _repo.GetLastAsync(orderByColumn: nameof(Branches.CreatedAt), cancellationToken: HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/Branchescrud/random
        [HttpGet("random")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<Branches?>>> GetRandom()
        {
            var res = await _repo.GetRandomAsync(HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // PUT: api/sales/Branchescrud/active
        // Body: { "id": "GUID", "isActive": true }
        [HttpPut("activeDapper")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.Activate)]
        public async Task<ActionResult<Result<bool>>> SetActiveByColumn([FromBody] ActiveBranchRequest dto)
        {
            if (!ModelState.IsValid)
                return StatusCode(400, Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId is null)
                return Unauthorized(Result<bool>.Fail(["User not authenticated."], 401));

            var entity = _mapper.MapActive<Branches, ActiveBranchRequest>(dto);
            var res = await _repo.SetActiveAsync(entity, dto.IsActive, userId.Value, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // أمثلة سريعة إضافية

        // GET: api/sales/Branchescrud/find?minCreated=2025-01-01
        [HttpGet("find")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
        public async Task<ActionResult<Result<List<Branches>>>> Find([FromQuery] DateTime? minCreated = null)
        {
            if (minCreated == null)
                return StatusCode(400, Result<List<Branches>>.Fail(new() { "minCreated is required." }, 400));

            var res = await _repo.FindAsync("CreatedAt >= @d", new { d = minCreated }, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/Branchescrud/count?active=true
        [HttpGet("count")]
        [RequirePermission(PermissionCatalogKeys.Pages.SalesBranches, PermissionCatalogKeys.Actions.View)]
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
