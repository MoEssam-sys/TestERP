using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using One.Application.Repos.Purchases;
using One.Application.Services.Mapping;
using One.Domain.Base;
using One.Domain.DTOs;
using One.Domain.DTOs.Purchases;
using One.Domain.Entities.Purchases;
using One.Domain.Enums;

namespace One.API.Controllers.Purchases
{
    [Authorize(Roles = "SuperAdmin")]
    [ApiController]
    [Route("api/sales/[controller]")]
    public class SuppliersController : BaseApiController
    {
        private readonly SuppliersRepo _repo;

        private readonly IDynamicMappingService _mapper;

        private readonly ILogger<SuppliersController> _logger;

        public SuppliersController(SuppliersRepo repo, IDynamicMappingService mapper, ILogger<SuppliersController> logger)
        {
            _repo = repo;
            _mapper = mapper;
            _logger = logger;
        }

        // GET: api/sales/suppliers
        [HttpGet]
        public async Task<ActionResult<Result<List<Suppliers>>>> GetAll()
        {
            var res = await _repo.GetAllAsync(HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // GET: api/sales/suppliers/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Result<Suppliers?>>> GetById(Guid id)
        {
            var res = await _repo.GetByIdAsync(id, HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // POST: api/sales/suppliers/create
        [HttpPost("create")]
        public async Task<ActionResult<Result<Suppliers>>> Create([FromBody] CreateSupplierRequest dto)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<Suppliers>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(Result<Suppliers>.Fail(new() { "User not authenticated." }, 401));

            // بنستخدم المابر الدايناميك → هيملأ الخصائص اللي عليها [InsertField]
            var entity = _mapper.MapCreate<Suppliers, CreateSupplierRequest>(dto);

            var insertRes = await _repo.InsertAndReturnAsync(entity, userId.Value, cancellationToken: HttpContext.RequestAborted);
            if (!insertRes.Success)
                return ToActionResult(Result<Suppliers>.Fail(insertRes.Messages ?? new() { "Insert failed." },
                                                          insertRes.StatusCode ?? 400));

            return ToActionResult(insertRes, statusOverride: 201);
        }

        // PUT: api/sales/suppliers/update
        [HttpPut("update")]
        public async Task<ActionResult<Result<bool>>> Update([FromBody] UpdateSupplierRequest dto)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(Result<bool>.Fail(new() { "User not authenticated." }, 401));

            // المابر الدايناميك هيملأ [UpdateField] + Id
            var entity = _mapper.MapUpdate<Suppliers, UpdateSupplierRequest>(dto);

            var res = await _repo.UpdateAsync(entity, userId.Value, cancellationToken: HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // DELETE: api/sales/suppliers/delete
        [HttpDelete("delete")]
        public async Task<ActionResult<Result<bool>>> Delete(
            [FromBody] DeleteEntityRequest<Guid> request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return StatusCode(400, Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId is null)
                return Unauthorized(Result<bool>.Fail(["User not authenticated."], 401));

            var res = await _repo.DeleteAsync(request, userId.Value, cancellationToken);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // PUT: api/sales/suppliers/active
        [HttpPut("active")]
        public async Task<ActionResult<Result<bool>>> SetActive([FromBody] ActiveSupplierRequest dto)
        {
            if (!ModelState.IsValid)
                return ToActionResult(Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(Result<bool>.Fail(new() { "User not authenticated." }, 401));

            // المابر ينسخ الـ Id (وأي مفاتيح ActiveField أخرى لو موجودة) + نمرر IsActive من الـ DTO
            var keyEntity = _mapper.MapActive<Suppliers, ActiveSupplierRequest>(dto);

            var res = await _repo.SetActiveAsync(keyEntity, dto.IsActive, userId.Value, HttpContext.RequestAborted);
            return ToActionResult(res);
        }

        // Change the method signature to:
        [HttpPost("filter")]
        public async Task<ActionResult<Result<PagedResult<Suppliers>>>> Filter([FromBody] FilterSupplierRequest filter)
        {
            if (!ModelState.IsValid)
                return ToPagedActionResult(Result<PagedResult<Suppliers>>.Fail(new() { "Validation failed." }, 400));

            var res = await _repo.QueryByFilterAsync(filter, HttpContext.RequestAborted);
            return ToPagedActionResult(res);
        }

        // GET: api/sales/suppliers/firstOrDefault
        [HttpGet("firstOrDefault")]
        public async Task<ActionResult<Result<Suppliers?>>> FirstOrDefaultAsync([FromBody] Guid id)
        {
            var res = await _repo.FirstOrDefaultAsync("Id = @id", new { id }, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/suppliers/exists/code
        [HttpGet("exists/code")]
        public async Task<ActionResult<Result<bool>>> IsCodeUnique([FromBody] string code)
        {
            var res = await _repo.IsUniqueAsync(nameof(Suppliers.Code), code, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // POST: api/sales/suppliers/search
        [HttpPost("search")]
        public async Task<ActionResult<Result<List<Suppliers>>>> Search([FromBody] string query)
        {
            var res = await _repo.SearchAsync(
                columns: new[] { nameof(Suppliers.Name), nameof(Suppliers.Code) },
                query: query,
                maxResults: 20,
                likeCond: LikeConditions.OR,
                cancellationToken: HttpContext.RequestAborted
            );
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // POST: api/sales/suppliers/ids
        [HttpPost("ids")]
        public async Task<ActionResult<Result<List<Suppliers>>>> GetByIds([FromBody] IEnumerable<Guid> ids)
        {
            var res = await _repo.GetByIdsAsync(ids, cancellationToken: HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/suppliers/last
        [HttpGet("last")]
        public async Task<ActionResult<Result<Suppliers?>>> GetLast()
        {
            // تقدر تغيّر العمود حسب ترتيبك: CreatedAt/Id/Code...
            var res = await _repo.GetLastAsync(orderByColumn: nameof(Suppliers.CreatedAt), cancellationToken: HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/suppliers/random
        [HttpGet("random")]
        public async Task<ActionResult<Result<Suppliers?>>> GetRandom()
        {
            var res = await _repo.GetRandomAsync(HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // PUT: api/sales/suppliers/activeByColumn
        // Body: { "id": "GUID", "isActive": true }
        [HttpPut("activeByColumn")]
        public async Task<ActionResult<Result<bool>>> SetActiveByColumn([FromBody] ActiveSupplierRequest dto)
        {
            if (!ModelState.IsValid)
                return StatusCode(400, Result<bool>.Fail(new() { "Validation failed." }, 400));

            var userId = GetAuthenticatedUserId();
            if (userId is null)
                return Unauthorized(Result<bool>.Fail(["User not authenticated."], 401));

            var entity = _mapper.MapActive<Suppliers, ActiveSupplierRequest>(dto);
            var res = await _repo.SetActiveAsync(entity, dto.IsActive, userId.Value, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // أمثلة سريعة إضافية
        // GET: api/sales/suppliers/find
        [HttpGet("find")]
        public async Task<ActionResult<Result<List<Suppliers>>>> Find([FromBody] DateTime? minCreated = null)
        {
            if (minCreated == null)
                return StatusCode(400, Result<List<Suppliers>>.Fail(new() { "minCreated is required." }, 400));

            var res = await _repo.FindAsync("CreatedAt >= @d", new { d = minCreated }, HttpContext.RequestAborted);
            return StatusCode(res.StatusCode ?? (res.Success ? 200 : 400), res);
        }

        // GET: api/sales/suppliers/count?active=true
        [HttpGet("count")]
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
