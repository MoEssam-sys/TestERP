using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using One.Domain.Base;
using One.Domain.Constants;
using One.Domain.DTOs.Logging;
using One.Domain.Entities.Identity;
using One.Infrastructure.DbContext.UsersDbContext;
using One.API.Security;
using System.Security.Claims;
using System.Linq.Expressions;

namespace One.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LogsController : ControllerBase
    {
        private readonly UsersDbContext _context;

        public LogsController(UsersDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [AllowAnonymous]
        [RequestSizeLimit(64 * 1024)]
        public async Task<IActionResult> CreateLog(
            [FromBody] LogDto dto,
            CancellationToken cancellationToken)
        {
            var company = await ResolveCompanyAsync(cancellationToken);
            await _context.AppLogs.AddAsync(CreateLogEntity(dto, company), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, data = true });
        }

        [HttpPost("batch")]
        [AllowAnonymous]
        [RequestSizeLimit(256 * 1024)]
        public async Task<IActionResult> CreateLogsBatch(
            [FromBody] List<LogDto> dtos,
            CancellationToken cancellationToken)
        {
            if (dtos.Count == 0)
                return Ok(new { success = true, data = true });
            if (dtos.Count > 50)
                return BadRequest(new { success = false, message = "A log batch cannot exceed 50 entries." });

            var company = await ResolveCompanyAsync(cancellationToken);
            await _context.AppLogs.AddRangeAsync(
                dtos.Select(dto => CreateLogEntity(dto, company)),
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, data = true });
        }

        [HttpGet("admin")]
        [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
        public async Task<ActionResult<Result<PagedResult<AppLogDto>>>> GetAdminLogs(
            [FromQuery] AppLogQueryDto query,
            CancellationToken cancellationToken)
        {
            var logs = _context.AppLogs.AsNoTracking().AsQueryable();
            if (query.CompanyId.HasValue)
                logs = logs.Where(log => log.CompanyId == query.CompanyId.Value);
            if (query.Severity.HasValue)
                logs = logs.Where(log => log.Severity == query.Severity.Value);
            if (query.Category.HasValue)
                logs = logs.Where(log => log.Category == query.Category.Value);
            if (query.FromUtc.HasValue)
                logs = logs.Where(log => log.OccurredAtUtc >= NormalizeUtc(query.FromUtc.Value));
            if (query.ToUtc.HasValue)
                logs = logs.Where(log => log.OccurredAtUtc <= NormalizeUtc(query.ToUtc.Value));
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                logs = logs.Where(log =>
                    log.Message.Contains(search) ||
                    (log.ReferenceNumber != null && log.ReferenceNumber.Contains(search)) ||
                    (log.CompanyName != null && log.CompanyName.Contains(search)) ||
                    (log.UserName != null && log.UserName.Contains(search)) ||
                    (log.Route != null && log.Route.Contains(search)) ||
                    (log.FunctionName != null && log.FunctionName.Contains(search)));
            }

            var totalCount = await logs.CountAsync(cancellationToken);
            var items = await logs
                .OrderByDescending(log => log.OccurredAtUtc)
                .ThenByDescending(log => log.Id)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(LogProjection)
                .ToListAsync(cancellationToken);
            return Ok(Result<PagedResult<AppLogDto>>.Ok(new PagedResult<AppLogDto>(
                items,
                totalCount,
                query.PageNumber,
                query.PageSize)));
        }

        [HttpGet("admin/{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.ManageCompanies)]
        public async Task<ActionResult<Result<AppLogDto>>> GetAdminLog(
            int id,
            CancellationToken cancellationToken)
        {
            var log = await _context.AppLogs
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(LogProjection)
                .FirstOrDefaultAsync(cancellationToken);
            return log is null
                ? NotFound(Result<AppLogDto>.Fail(["Error log was not found."], 404))
                : Ok(Result<AppLogDto>.Ok(log));
        }

        private AppLog CreateLogEntity(LogDto dto, Company? company)
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            var companyId = int.TryParse(companyIdClaim, out var parsedCompanyId)
                ? parsedCompanyId
                : (int?)null;
            return new AppLog
            {
                Severity = dto.Severity,
                Category = dto.Category,
                UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                UserName = User.FindFirst(ClaimTypes.Name)?.Value,
                CompanyId = companyId,
                CompanyName = company?.Name,
                Route = SensitiveLogSanitizer.SanitizeOptional(dto.Route, 500),
                Action = SensitiveLogSanitizer.SanitizeOptional(dto.Action, 200),
                FunctionName = SensitiveLogSanitizer.SanitizeOptional(dto.FunctionName, 500),
                SourceFile = SensitiveLogSanitizer.SanitizeOptional(
                    string.IsNullOrWhiteSpace(dto.SourceFile) ? null : Path.GetFileName(dto.SourceFile),
                    260),
                LineNumber = dto.LineNumber,
                Message = SensitiveLogSanitizer.Sanitize(dto.Message, 2000),
                ExceptionType = SensitiveLogSanitizer.SanitizeOptional(dto.ExceptionType, 300),
                StackTrace = SensitiveLogSanitizer.SanitizeOptional(dto.StackTrace, 8000),
                Hash = SensitiveLogSanitizer.SanitizeOptional(dto.Hash, 128),
                Platform = SensitiveLogSanitizer.SanitizeOptional(dto.Platform, 100),
                DeviceModel = SensitiveLogSanitizer.SanitizeOptional(dto.DeviceModel, 300),
                AppVersion = SensitiveLogSanitizer.SanitizeOptional(dto.AppVersion, 100),
                MetadataJson = SensitiveLogSanitizer.SanitizeOptional(dto.MetadataJson, 4000),
                ReferenceNumber = CreateReferenceNumber(),
                OccurredAtUtc = ResolveOccurredAtUtc(dto.ClientTimestampUtc),
                OccurredAtLocal = dto.ClientTimestampLocal ?? DateTimeOffset.Now,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        private async Task<Company?> ResolveCompanyAsync(CancellationToken cancellationToken)
        {
            if (!int.TryParse(User.FindFirst("companyId")?.Value, out var companyId))
                return null;

            return await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(company => company.Id == companyId, cancellationToken);
        }

        private static readonly Expression<Func<AppLog, AppLogDto>> LogProjection = log => new AppLogDto(
            log.Id,
            log.ReferenceNumber ?? string.Empty,
            log.Severity,
            log.Category,
            log.CompanyId,
            log.CompanyName,
            log.UserId,
            log.UserName,
            log.Route,
            log.RequestMethod,
            log.FunctionName ?? log.Action,
            log.SourceFile,
            log.LineNumber,
            log.StatusCode,
            log.CorrelationId,
            log.Message,
            log.ExceptionType,
            log.StackTrace,
            log.Platform,
            log.DeviceModel,
            log.AppVersion,
            log.OccurredAtUtc,
            log.OccurredAtLocal,
            log.CreatedAtUtc);

        private static string CreateReferenceNumber()
        {
            var suffix = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            return $"ONE-{suffix}";
        }

        private static DateTime ResolveOccurredAtUtc(DateTime clientTimestampUtc)
        {
            var now = DateTime.UtcNow;
            if (clientTimestampUtc == default)
                return now;

            var normalized = NormalizeUtc(clientTimestampUtc);
            return normalized < now.AddDays(-7) || normalized > now.AddHours(1)
                ? now
                : normalized;
        }

        private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
