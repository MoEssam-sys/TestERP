using Dapper;
using Microsoft.Extensions.Logging;
using One.Domain.Base;
using One.Domain.DTOs;
using One.Domain.Enums;
using One.Domain.Services.Attributes;
using One.Domain.Tenancy;
using One.Infrastructure.DbContext.MainDbContext;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace One.Application.Base
{
    public abstract class BaseRepository<T, TKey> where T : BaseEntity<TKey>, new()
    {
        private const int MaxListSize = 100;
        protected readonly IDapperConnectionFactory dbFactory;
        private readonly ITenantContext tenantContext;
        private readonly ILogger logger;
        protected readonly string tableName;           // الاسم المنطقي (قد يتضمن schema)
        protected readonly string tableNameSql;        // الاسم مؤهَّل بالأقواس [] لاستخدامه في SQL

        // Fix #11: Compiled whitelist regex — only safe SQL identifiers pass
        private static readonly Regex _safeIdentifierRegex =
            new(@"^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.Compiled);

        // Fix #10: Cache PropertyInfo lookups per filter type
        private static readonly ConcurrentDictionary<Type, (PropertyInfo? pageNumber, PropertyInfo? pageSize)>
            _filterPropCache = new();

        // ===== Ctor =====
        protected BaseRepository(
            IDapperConnectionFactory dbFactory,
            ITenantContext tenantContext,
            ILogger _logger)
        {
            this.dbFactory = dbFactory;
            this.tenantContext = tenantContext;
            logger = _logger;

            if (typeof(T).GetCustomAttribute<ReferenceDataAttribute>() is null)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} is transactional and cannot use BaseRepository. " +
                    "Use a dedicated application service and transaction boundary.");
            }

            if (typeof(T).GetProperty("IsActive") is null ||
                typeof(T).GetProperties().All(property =>
                    property.GetCustomAttribute<ConcurrencyFieldAttribute>() is null))
            {
                throw new InvalidOperationException(
                    $"Reference entity {typeof(T).Name} requires IsActive and RowVersion fields.");
            }

            var t = typeof(T);
            var tbl = t.GetCustomAttribute<DbTableAttribute>()?.Name;
            var resolved = string.IsNullOrWhiteSpace(tbl) ? t.Name : tbl!;

            tableName = !string.IsNullOrWhiteSpace(TableNameOverride) ? TableNameOverride! : resolved;
            tableNameSql = Qualify(tableName); // "[dbo].[Table]" أو "[Table]"

        }

        /// <summary>اختياري: لو حابب تغيّر اسم الجدول يدويًا بدل [DbTable] على الكلاس.</summary>
        protected virtual string? TableNameOverride => null;

        protected abstract IReadOnlySet<string> AllowedQueryColumns { get; }


        // ----------------- READ -----------------
        public virtual async Task<Result<T?>> GetByIdAsync(
            TKey id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var db = dbFactory.CreateConnection();
                var (sql, dp) = AttributeCrudEngine.BuildGetByIdSql<T, TKey>(id);
                var scopedParameters = WithTenantParameters(dp);
                sql = AddTenantPredicate(sql);
                LogSql(sql, scopedParameters);
                var row = await db.QueryFirstOrDefaultAsync<T>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken));
                return Result<T?>.Ok(row);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(ex, "retrieve {Entity} by id", typeof(T).Name);
                return Result<T?>.Fail(new() { "An error occurred while retrieving the record." });
            }
        }

        private int CompanyId => tenantContext.CompanyId;

        private DynamicParameters WithTenantParameters(object? parameters = null)
        {
            var scoped = new DynamicParameters();
            if (parameters is not null)
                scoped.AddDynamicParams(parameters);

            scoped.Add("@__TenantCompanyId", CompanyId);
            return scoped;
        }

        private string AddTenantPredicate(string sql)
        {
            var normalized = sql.Trim().TrimEnd(';').Trim();
            var orderMatch = Regex.Match(normalized, @"\border\s+by\b", RegexOptions.IgnoreCase);
            var statement = orderMatch.Success ? normalized[..orderMatch.Index].TrimEnd() : normalized;
            var orderBy = orderMatch.Success ? " " + normalized[orderMatch.Index..] : string.Empty;

            statement += Regex.IsMatch(statement, @"\bwhere\b", RegexOptions.IgnoreCase)
                ? " AND [CompanyId] = @__TenantCompanyId"
                : " WHERE [CompanyId] = @__TenantCompanyId";

            return statement + orderBy + ";";
        }

        public virtual async Task<Result<List<T>>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var db = dbFactory.CreateConnection();
                // Fix #6: Safety limit — prevents unbounded full-table scans
                var sql = $"SELECT TOP ({MaxListSize}) " +
                    AttributeCrudEngine.BuildGetAllSql<T>().TrimStart().Substring("SELECT ".Length);
                var scopedParameters = WithTenantParameters();
                sql = AddTenantPredicate(sql);
                sql = sql.TrimEnd(';') + " ORDER BY [CreatedAt] DESC, [Id] DESC;";
                LogSql(sql, scopedParameters);
                var list = (await db.QueryAsync<T>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken))).AsList();
                return Result<List<T>>.Ok(list);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(ex, "retrieve {Entity} list", typeof(T).Name);
                return Result<List<T>>.Fail(new() { "An error occurred while retrieving records." });
            }
        }

        public virtual async Task<Result<TKey>> InsertAsync(
            T entity,
            Guid currentUserId,
            Func<IDbConnection, IDbTransaction, TKey, Task<Result<bool>>>? extraActions = null,
            CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateConnection();
            if (db.State != ConnectionState.Open)
                db.Open();
            using var tran = db.BeginTransaction();

            try
            {
                if (entity is BaseEntity<TKey> baseEntity)
                {
                    baseEntity.CompanyId = CompanyId;
                    baseEntity.CreatedAt = DateTime.UtcNow;
                    baseEntity.CreatedBy = currentUserId;
                }

                var errors = AttributeCrudEngine.ValidateRequired(entity!, isInsert: true);
                if (errors.Count > 0)
                {
                    tran.Rollback();
                    return Result<TKey>.Fail(errors.Cast<string?>().ToList(), 400);
                }

                var (sql, dp, _) = AttributeCrudEngine.BuildInsertSql<TKey>(entity!);
                LogSql(sql, dp);
                var id = await db.ExecuteScalarAsync<TKey>(new CommandDefinition(
                    sql,
                    dp,
                    tran,
                    cancellationToken: cancellationToken));

                if (id == null)
                {
                    tran.Rollback();
                    return Result<TKey>.Fail(new() { "Insert did not return a new id." }, 500);
                }

                if (extraActions != null)
                {
                    var more = await extraActions(db, tran, id);
                    if (!more.Success)
                    {
                        tran.Rollback();
                        return Result<TKey>.Fail(more.Messages ?? new() { "Post-insert actions failed." });
                    }
                }

                tran.Commit();
                return Result<TKey>.Ok(id);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                tran.Rollback();
                LogFailure(ex, "insert {Entity}", typeof(T).Name);
                return Result<TKey>.Fail(new() { "An error occurred while inserting the record." });
            }
        }

        public virtual async Task<Result<T>> InsertAndReturnAsync(
            T entity,
            Guid currentUserId,
            Func<IDbConnection, IDbTransaction, TKey, Task<Result<bool>>>? extraActions = null,
            CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateConnection();
            if (db.State != ConnectionState.Open)
                db.Open();
            using var tran = db.BeginTransaction();

            try
            {
                if (entity is BaseEntity<TKey> baseEntity)
                {
                    baseEntity.CompanyId = CompanyId;
                    baseEntity.CreatedAt = DateTime.UtcNow;
                    baseEntity.CreatedBy = currentUserId;
                }

                var errors = AttributeCrudEngine.ValidateRequired(entity!, isInsert: true);
                if (errors.Count > 0)
                {
                    tran.Rollback();
                    return Result<T>.Fail(errors.Cast<string?>().ToList(), 400);
                }

                var (insSql, dp, _) = AttributeCrudEngine.BuildInsertSql<TKey>(entity!);
                LogSql(insSql, dp);
                var id = await db.ExecuteScalarAsync<TKey>(new CommandDefinition(
                    insSql,
                    dp,
                    tran,
                    cancellationToken: cancellationToken));

                if (id == null)
                {
                    tran.Rollback();
                    return Result<T>.Fail(new() { "Insert did not return a new id." }, 500);
                }

                if (extraActions != null)
                {
                    var more = await extraActions(db, tran, id);
                    if (!more.Success)
                    {
                        tran.Rollback();
                        return Result<T>.Fail(more.Messages ?? new() { "Post-insert actions failed." });
                    }
                }

                var (selSql, selDp) = AttributeCrudEngine.BuildGetByIdSql<T, TKey>(id);
                var scopedSelectParameters = WithTenantParameters(selDp);
                selSql = AddTenantPredicate(selSql);
                LogSql(selSql, scopedSelectParameters);
                var created = await db.QueryFirstOrDefaultAsync<T>(new CommandDefinition(
                    selSql,
                    scopedSelectParameters,
                    tran,
                    cancellationToken: cancellationToken));

                if (created == null)
                {
                    tran.Rollback();
                    return Result<T>.Fail(new() { "Inserted but no row returned by GetById." }, 500);
                }

                tran.Commit();

                return Result<T>.Ok(created);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                tran.Rollback();
                LogFailure(ex, "insert and return {Entity}", typeof(T).Name);
                return Result<T>.Fail(new() { "An error occurred while inserting the record." });
            }
        }

        // ----------------- UPDATE -----------------
        public virtual async Task<Result<bool>> UpdateAsync(
            T entity,
            Guid currentUserId,
            Func<IDbConnection, IDbTransaction, T, Task<Result<bool>>>? extraActions = null,
            CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateConnection();
            if (db.State != ConnectionState.Open)
                db.Open();
            using var tran = db.BeginTransaction();

            try
            {
                if (entity is BaseEntity<TKey> baseEntity)
                {
                    baseEntity.CompanyId = CompanyId;
                    baseEntity.UpdatedAt = DateTime.UtcNow;
                    baseEntity.UpdatedBy = currentUserId;
                }

                if (!HasRowVersion(entity))
                {
                    tran.Rollback();
                    return Result<bool>.Fail(["A row version is required. Reload the record and try again."], 428);
                }

                var errors = AttributeCrudEngine.ValidateRequired(entity!, isInsert: false);
                if (errors.Count > 0)
                {
                    tran.Rollback();
                    return Result<bool>.Fail(errors.Cast<string?>().ToList(), 400);
                }

                var (sql, dp) = AttributeCrudEngine.BuildUpdateSql(entity!);
                var scopedParameters = WithTenantParameters(dp);
                sql = AddTenantPredicate(sql);
                LogSql(sql, scopedParameters);
                var rows = await db.ExecuteAsync(new CommandDefinition(
                    sql,
                    scopedParameters,
                    tran,
                    cancellationToken: cancellationToken));

                if (rows <= 0)
                {
                    var failure = await ConflictOrNotFoundAsync(db, tran, entity.Id, cancellationToken);
                    tran.Rollback();
                    return failure;
                }

                if (extraActions != null)
                {
                    var more = await extraActions(db, tran, entity);
                    if (!more.Success)
                    {
                        tran.Rollback();
                        return Result<bool>.Fail(more.Messages ?? new() { "Post-update actions failed." });
                    }
                }

                tran.Commit();
                return Result<bool>.Ok(true);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                tran.Rollback();
                LogFailure(ex, "update {Entity}", typeof(T).Name);
                return Result<bool>.Fail(new() { "An error occurred while updating the record." });
            }
        }

        // ----------------- DELETE -----------------
        /// <summary>حذف باستخدام Id فقط (لو موجود).</summary>
        public virtual async Task<Result<bool>> DeleteAsync(
            DeleteEntityRequest<TKey> request,
            Guid updatedBy,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (request.RowVersion is not { Length: > 0 })
                    return Result<bool>.Fail(["A row version is required. Reload the record and try again."], 428);

                using var db = dbFactory.CreateConnection();
                var sql = $"""
                    UPDATE {tableNameSql}
                    SET [IsActive] = 0,
                        [UpdatedAt] = @updatedAt,
                        [UpdatedBy] = @updatedBy
                    WHERE [CompanyId] = @__TenantCompanyId
                      AND [Id] = @id
                      AND [RowVersion] = @rowVersion;
                    """;
                var scopedParameters = WithTenantParameters(new
                {
                    id = request.Id,
                    rowVersion = request.RowVersion,
                    updatedAt = DateTime.UtcNow,
                    updatedBy
                });
                LogSql(sql, scopedParameters);
                var rows = await db.ExecuteAsync(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken));

                return rows > 0 ? Result<bool>.Ok(true)
                                : await ConflictOrNotFoundAsync(db, null, request.Id, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(ex, "soft delete {Entity}", typeof(T).Name);
                return Result<bool>.Fail(new() { "An error occurred while deleting the record." });
            }
        }

        // ----------------- ACTIVATE / DEACTIVATE -----------------
        /// <summary>تفعيل/تعطيل باستخدام [ActiveField]/[DeleteField] أو Id.</summary>
        public virtual async Task<Result<bool>> SetActiveAsync(
            T entityKeyOnly,
            bool isActive,
            Guid updatedBy,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (entityKeyOnly is BaseEntity<TKey> be)
                {
                    be.UpdatedAt = DateTime.UtcNow;
                    be.UpdatedBy = updatedBy;
                }

                if (!HasRowVersion(entityKeyOnly))
                    return Result<bool>.Fail(["A row version is required. Reload the record and try again."], 428);

                using var db = dbFactory.CreateConnection();
                var (sql, dp) = AttributeCrudEngine.BuildSetActiveSql(entityKeyOnly!, isActive);
                var scopedParameters = WithTenantParameters(dp);
                sql = AddTenantPredicate(sql);
                LogSql(sql, scopedParameters);
                var rows = await db.ExecuteAsync(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken));

                return rows > 0 ? Result<bool>.Ok(true)
                                : await ConflictOrNotFoundAsync(db, null, entityKeyOnly.Id, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(ex, "change {Entity} status", typeof(T).Name);
                return Result<bool>.Fail(new() { "An error occurred while updating the record status." });
            }
        }

        // ----------------- SELECT with Filter -----------------
        /// <summary>أي كلاس فلتر عليه [DbTable] + [MatchOn]/[MatchDictionary]</summary>
        public virtual async Task<Result<PagedResult<T>>> QueryByFilterAsync<TFilter>(
            TFilter filter,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (filter is null)
                    return Result<PagedResult<T>>.Fail(["A filter is required."], 400);

                if (!ValidateFilterColumns(filter, out var filterError))
                    return Result<PagedResult<T>>.Fail([filterError], 400);

                using var db = dbFactory.CreateConnection();

                // 1) SQL الأساسي
                var (baseSqlRaw, dp) = AttributeCrudEngine.BuildSelectByFilterSql(filter!);
                var scopedParameters = WithTenantParameters(dp);
                baseSqlRaw = AddTenantPredicate(baseSqlRaw);

                // 2) شيل ; وأي ORDER BY نهائي لتجنّب "Incorrect syntax near 'ORDER'"
                var baseSql = baseSqlRaw.Trim();
                if (baseSql.EndsWith(";")) baseSql = baseSql[..^1].Trim();
                baseSql = Regex.Replace(baseSql, @"\border\s+by\b[\s\S]*$", string.Empty, RegexOptions.IgnoreCase).Trim();

                // 3) إجمالي العدّ
                var countSql = $"SELECT COUNT_BIG(1) FROM ({baseSql}) AS _c";
                var totalCount = await db.ExecuteScalarAsync<long>(new CommandDefinition(
                    countSql,
                    scopedParameters,
                    cancellationToken: cancellationToken));

                // 4) صفحة/حجم
                int pageNumber = 1, pageSize = 10;
                // Fix #10: Use cached PropertyInfo to avoid reflection on every request
                var (pnProp, psProp) = _filterPropCache.GetOrAdd(filter!.GetType(), t =>
                    (t.GetProperty("PageNumber"), t.GetProperty("PageSize")));

                if (pnProp?.PropertyType == typeof(int))
                    pageNumber = Math.Clamp(
                        (int)(pnProp.GetValue(filter) ?? 1),
                        1,
                        1_000_000);

                if (psProp?.PropertyType == typeof(int))
                    pageSize = Math.Clamp(
                        (int)(psProp.GetValue(filter) ?? 10),
                        1,
                        BaseFilter.MaxPageSize);

                // 5) ترتيب افتراضي قبل OFFSET/FETCH
                var requestedSort = filter is BaseFilter baseFilter ? baseFilter.SortBy : null;
                var sortColumn = string.IsNullOrWhiteSpace(requestedSort)
                    ? (HasProp(typeof(T), "CreatedAt") ? "CreatedAt" : "Id")
                    : ResolvePropertyToColumn(requestedSort);
                if (!IsAllowedQueryColumn(sortColumn))
                    return Result<PagedResult<T>>.Fail(["The requested sort column is not allowed."], 400);

                var direction = filter is BaseFilter { SortDescending: false } ? "ASC" : "DESC";
                var orderBy = $"[{sortColumn}] {direction}" +
                    (string.Equals(sortColumn, "Id", StringComparison.OrdinalIgnoreCase)
                        ? string.Empty
                        : ", [Id] DESC");

                var pagedSql = $"{baseSql} ORDER BY {orderBy} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                var dpPaged = new DynamicParameters(scopedParameters);
                dpPaged.Add("@Offset", checked((pageNumber - 1) * pageSize));
                dpPaged.Add("@PageSize", pageSize);

                LogSql(pagedSql, dpPaged);

                var items = (await db.QueryAsync<T>(new CommandDefinition(
                    pagedSql,
                    dpPaged,
                    cancellationToken: cancellationToken))).AsList();

                var paged = new PagedResult<T>(
                    items: items,
                    totalCount: (int)Math.Min(totalCount, int.MaxValue),
                    pageNumber: pageNumber,
                    pageSize: pageSize,
                    filters: null, sorts: null, metadata: null
                );

                return Result<PagedResult<T>>.Ok(paged);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(ex, "query {Entity} records", typeof(T).Name);
                return Result<PagedResult<T>>.Fail(new() { "An error occurred while querying records." });
            }

            static bool HasProp(Type t, string name)
                => t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) != null;
        }

        #region ----- Paging / Query helpers -----

        public async Task<Result<List<T>>> FindAsync(
            string whereSql,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = dbFactory.CreateConnection();
                var scopedParameters = WithTenantParameters(parameters);
                var sql = $"SELECT TOP ({MaxListSize}) * FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId AND ({whereSql}) ORDER BY [CreatedAt] DESC, [Id] DESC";
                var list = (await conn.QueryAsync<T>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken))).ToList();
                return Result<List<T>>.Ok(list);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "find {Entity} records", typeof(T).Name);
                return Result<List<T>>.Fail(["حدث خطأ أثناء البحث."]);
            }
        }

        public async Task<Result<bool>> IsExistAsync(
            string whereSql,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = dbFactory.CreateConnection();
                var scopedParameters = WithTenantParameters(parameters);
                var sql = $"SELECT 1 FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId AND ({whereSql})";
                var exists = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken));
                return Result<bool>.Ok(exists.HasValue);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "check {Entity} existence", typeof(T).Name);
                return Result<bool>.Fail(["تعذر التحقق من وجود العنصر."]);
            }
        }

        public async Task<Result<int>> CountAsync(
            string whereSql = "1=1",
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = dbFactory.CreateConnection();
                var scopedParameters = WithTenantParameters(parameters);
                var sql = $"SELECT COUNT(*) FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId AND ({whereSql})";
                var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken));
                return Result<int>.Ok(count);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "count {Entity} records", typeof(T).Name);
                return Result<int>.Fail(["تعذر حساب العدد."]);
            }
        }

        public async Task<Result<T?>> FirstOrDefaultAsync(
            string whereSql,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = dbFactory.CreateConnection();
                var scopedParameters = WithTenantParameters(parameters);
                var sql = $"SELECT TOP 1 * FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId AND ({whereSql}) ORDER BY [CreatedAt] DESC, [Id] DESC";
                var entity = await conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken));
                return Result<T?>.Ok(entity);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "retrieve first {Entity}", typeof(T).Name);
                return Result<T?>.Fail(["تعذر استرجاع العنصر."]);
            }
        }

        public async Task<Result<List<T>>> GetByIdsAsync(
            IEnumerable<TKey> ids,
            string? idColumn = "Id",
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (ids is null)
                    return Result<List<T>>.Fail(["قائمة المعرفات فارغة."]);

                var idsList = ids.ToList();
                if (idsList.Count == 0)
                    return Result<List<T>>.Ok(new List<T>());

                if (idsList.Count > MaxListSize)
                    return Result<List<T>>.Fail([$"A request cannot contain more than {MaxListSize} ids."], 400);

                if (!IsAllowedQueryColumn(idColumn))
                    return Result<List<T>>.Fail(["اسم العمود غير صالح."]);

                using var conn = dbFactory.CreateConnection();
                var scopedParameters = WithTenantParameters(new { ids = idsList });
                var sql = $"SELECT * FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId AND [{idColumn}] IN @ids";
                sql += " ORDER BY [CreatedAt] DESC, [Id] DESC";
                var result = (await conn.QueryAsync<T>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken))).ToList();
                return Result<List<T>>.Ok(result);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "retrieve {Entity} ids", typeof(T).Name);
                return Result<List<T>>.Fail(["تعذر جلب العناصر المطلوبة."]);
            }
        }

        #endregion ----- Paging / Query helpers -----

        /// <summary>Soft-Delete: تعيين IsActive = 0 بالمعرّف</summary>
        public async Task<Result<bool>> DeleteByColumnAsync(object id, string? idColumn = "Id")
        {
            await Task.CompletedTask;
            return Result<bool>.Fail(
                ["Unsafe column-based deletion is disabled. Use the row-version delete contract."],
                403);
        }

        public async Task<Result<bool>> SetActiveByColumnAsync(object id, bool isActive, string? idColumn = "Id")
        {
            await Task.CompletedTask;
            return Result<bool>.Fail(
                ["Unsafe column-based status updates are disabled. Use the row-version status contract."],
                403);
        }

        public async Task<Result<List<T>>> GetByIdsByColumnAsync(
            IEnumerable<object> ids,
            string? idColumn = "Id",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var idList = ids?.Take(MaxListSize + 1).ToList() ?? [];
                if (idList.Count > MaxListSize)
                    return Result<List<T>>.Fail([$"A request cannot contain more than {MaxListSize} ids."], 400);

                if (!IsAllowedQueryColumn(idColumn))
                    return Result<List<T>>.Fail(["اسم العمود غير صالح."]);

                using var conn = dbFactory.CreateConnection();
                var scopedParameters = WithTenantParameters(new { ids = idList });
                var sql = $"SELECT * FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId AND [{idColumn}] IN @ids ORDER BY [CreatedAt] DESC, [Id] DESC";
                var result = (await conn.QueryAsync<T>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken))).ToList();
                return Result<List<T>>.Ok(result);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "retrieve {Entity} by dynamic ids", typeof(T).Name);
                return Result<List<T>>.Fail(["تعذر جلب العناصر المطلوبة."]);
            }
        }

        public async Task<Result<T?>> GetLastAsync(
            string orderByColumn = "Id",
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!IsAllowedQueryColumn(orderByColumn))
                    return Result<T?>.Fail(["اسم عمود الترتيب غير صالح."]);

                using var conn = dbFactory.CreateConnection();
                var scopedParameters = WithTenantParameters();
                var sql = $"SELECT TOP 1 * FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId ORDER BY [{orderByColumn}] DESC, [Id] DESC";
                var entity = await conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken));
                return Result<T?>.Ok(entity);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "retrieve last {Entity}", typeof(T).Name);
                return Result<T?>.Fail(["تعذر جلب العنصر الأخير."]);
            }
        }

        public async Task<Result<T?>> GetRandomAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = dbFactory.CreateConnection();
                var scopedParameters = WithTenantParameters();
                var sql = $"SELECT TOP 1 * FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId ORDER BY NEWID()";
                var entity = await conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken));
                return Result<T?>.Ok(entity);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "retrieve random {Entity}", typeof(T).Name);
                return Result<T?>.Fail(["تعذر جلب عنصر عشوائي."]);
            }
        }

        public async Task<Result<List<T>>> SearchAsync(
             string[] columns,
             string? query,
             int? maxResults = 20,
             LikeConditions likeCond = LikeConditions.OR,
             bool? isActive = true,                // null => هات الكل، true/false => فلترة
             Guid? firstId = null,
             string? firstIdColumn = null,
             Guid? secondId = null,
             string? secondIdColumn = null,
             CancellationToken cancellationToken = default
         )
        {
            try
            {
                // --- أعمدة البحث النصي ---
                if (columns == null || columns.Length == 0)
                    return Result<List<T>>.Fail(["لم يتم تحديد أعمدة البحث."]);

                var safeCols = columns
                    .Select(ResolvePropertyToColumn)             // <-- ترجمة property -> column
                    .Where(IsAllowedQueryColumn)
                    .Select(c => $"LOWER([{c}])")                // بحث بدون حساسية حالة الحروف
                    .ToArray();

                if (safeCols.Length == 0)
                    return Result<List<T>>.Fail(["أسماء الأعمدة غير صالحة."]);

                // --- فاليديشن الأعمدة/المعرفات التابعة (اختياريين) ---
                // FirstId
                if (!string.IsNullOrWhiteSpace(firstIdColumn))
                {
                    if (!IsAllowedQueryColumn(firstIdColumn))
                        return Result<List<T>>.Fail(["اسم العمود الأول (FirstIdColumn) غير صالح."]);

                    if (!firstId.HasValue || firstId.Value == Guid.Empty)
                        return Result<List<T>>.Fail(["تم تمرير FirstIdColumn بدون قيمة FirstId صحيحة."]);
                }
                else if (firstId.HasValue && firstId.Value != Guid.Empty)
                {
                    return Result<List<T>>.Fail(["تم تمرير FirstId بدون تحديد FirstIdColumn."]);
                }

                // SecondId
                if (!string.IsNullOrWhiteSpace(secondIdColumn))
                {
                    if (!IsAllowedQueryColumn(secondIdColumn))
                        return Result<List<T>>.Fail(["اسم العمود الثاني (SecondIdColumn) غير صالح."]);

                    if (!secondId.HasValue || secondId.Value == Guid.Empty)
                        return Result<List<T>>.Fail(["تم تمرير SecondIdColumn بدون قيمة SecondId صحيحة."]);
                }
                else if (secondId.HasValue && secondId.Value != Guid.Empty)
                {
                    return Result<List<T>>.Fail(["تم تمرير SecondId بدون تحديد SecondIdColumn."]);
                }

                // --- WHERE parts ---
                var conds = new List<string> { "[CompanyId] = @__TenantCompanyId" };
                var dp = WithTenantParameters();

                // بحث نصّي (لو فيه query)
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var q = query.Trim().ToLowerInvariant();
                    var likeParts = safeCols.Select((col, idx) => $"{col} LIKE @q{idx}").ToList();
                    for (int i = 0; i < likeParts.Count; i++)
                        dp.Add($"@q{i}", $"%{q}%");

                    var likeJoined = string.Join(likeCond == LikeConditions.OR ? " OR " : " AND ", likeParts);
                    conds.Add($"({likeJoined})");
                }

                // IsActive فلترة
                if (isActive.HasValue)
                {
                    conds.Add("[IsActive] = @isActive");
                    dp.Add("@isActive", isActive.Value);
                }

                // FirstId/SecondId
                if (!string.IsNullOrWhiteSpace(firstIdColumn))
                {
                    conds.Add($"[{firstIdColumn}] = @firstId");
                    dp.Add("@firstId", firstId!.Value);
                }

                if (!string.IsNullOrWhiteSpace(secondIdColumn))
                {
                    conds.Add($"[{secondIdColumn}] = @secondId");
                    dp.Add("@secondId", secondId!.Value);
                }

                // بناء SQL
                var whereSql = conds.Count > 0 ? " WHERE " + string.Join(" AND ", conds) : "";

                using var conn = dbFactory.CreateConnection();
                var take = Math.Clamp(maxResults ?? 20, 1, MaxListSize);
                var sql = $"SELECT TOP ({take}) * FROM {tableNameSql}{whereSql} ORDER BY [CreatedAt] DESC, [Id] DESC";

                var result = (await conn.QueryAsync<T>(new CommandDefinition(
                    sql,
                    dp,
                    cancellationToken: cancellationToken))).ToList();
                return Result<List<T>>.Ok(result);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "search {Entity} records", typeof(T).Name);
                return Result<List<T>>.Fail(["تعذر تنفيذ البحث."]);
            }
        }

        public async Task<Result<bool>> IsUniqueAsync(
            string column,
            object value,
            CancellationToken cancellationToken = default)
        {
            try
            {
                column = ResolvePropertyToColumn(column);
                if (!IsAllowedQueryColumn(column))
                    return Result<bool>.Fail(["اسم العمود غير صالح."]);

                var col = $"[{column}]";
                var sql = $"SELECT 1 FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId AND {col} = @value";

                var v = value is string s ? s.Trim() : value;

                using var conn = dbFactory.CreateConnection();
                var scopedParameters = WithTenantParameters(new { value = v });
                var exists = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
                    sql,
                    scopedParameters,
                    cancellationToken: cancellationToken));
                return Result<bool>.Ok(!exists.HasValue);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogFailure(exception, "check {Entity} uniqueness", typeof(T).Name);
                return Result<bool>.Fail(["تعذر التحقق من التفرد."]);
            }
        }

        public async Task<Result<bool>> UpdateByColumnAsync(string column, object value, object id, string? idColumn = "Id")
        {
            await Task.CompletedTask;
            return Result<bool>.Fail(
                ["Direct column updates are disabled. Use a validated update request with a row version."],
                403);
        }

        // ===== Bulk ops =====
        public async Task<Result<int>> BulkInsertAsync(IEnumerable<T> entities)
        {
            await Task.CompletedTask;
            return Result<int>.Fail(
                ["Bulk insert is disabled until a per-entity audited batch contract is provided."],
                403);
        }

        public async Task<Result<int>> BulkUpdateAsync(IEnumerable<T> entities)
        {
            await Task.CompletedTask;
            return Result<int>.Fail(
                ["التحديث الجماعي معطل حتى يتم تنفيذ عقد آمن داخل نطاق الشركة."],
                403);
        }

        public async Task<Result<int>> BulkDeleteAsync(IEnumerable<T> entities)
        {
            await Task.CompletedTask;
            return Result<int>.Fail(
                ["الحذف الجماعي معطل حتى يتم تنفيذ عقد آمن داخل نطاق الشركة."],
                403);
        }

        public async Task<Result<int>> BulkMergeAsync(IEnumerable<T> entities)
        {
            await Task.CompletedTask;
            return Result<int>.Fail(
                ["الدمج الجماعي معطل حتى يتم تنفيذ عقد آمن داخل نطاق الشركة."],
                403);
        }

        // ========== Helpers ==========
        // Fix #11: Whitelist-based validation — only safe identifier characters allowed
        public static bool IsValidSqlIdentifier(string? columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return false;

            return _safeIdentifierRegex.IsMatch(columnName);
        }

        private static string Qualify(string name)
        {
            // يحوّل "dbo.Table" -> "[dbo].[Table]" ، و"Table" -> "[Table]"
            var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join(".", parts.Select(p => p.StartsWith("[") && p.EndsWith("]") ? p : $"[{p}]"));
        }

        private static string ResolvePropertyToColumn(string propOrColumn)
        {
            // لو ده اسم property على T، ترجم لاسم العمود (يحترم [DbColumn])
            var p = typeof(T).GetProperty(propOrColumn,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (p != null)
            {
                var colAttr = p.GetCustomAttribute<DbColumnAttribute>();
                var col = string.IsNullOrWhiteSpace(colAttr?.Name) ? p.Name : colAttr!.Name!;
                return col;
            }

            // وإلا اعتبره بالفعل اسم عمود قادم جاهز
            return propOrColumn;
        }

        private bool IsAllowedQueryColumn(string? propertyOrColumn)
        {
            if (string.IsNullOrWhiteSpace(propertyOrColumn))
                return false;

            var resolved = ResolvePropertyToColumn(propertyOrColumn);
            return IsValidSqlIdentifier(resolved) && AllowedQueryColumns.Contains(resolved);
        }

        private bool ValidateFilterColumns<TFilter>(TFilter filter, out string error)
        {
            foreach (var property in filter!.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetCustomAttributes<MatchOnAttribute>(true).Any())
                {
                    var column = property.GetCustomAttribute<DbColumnAttribute>()?.Name ?? property.Name;
                    if (!IsAllowedQueryColumn(column))
                    {
                        error = $"Filter column '{column}' is not allowed for {typeof(T).Name}.";
                        return false;
                    }
                }

                if (!property.GetCustomAttributes<MatchDictionaryAttribute>(true).Any() ||
                    property.GetValue(filter) is not Dictionary<string, IEnumerable<object>> dictionary)
                {
                    continue;
                }

                if (dictionary.Count > 10 || dictionary.Keys.Any(key => !IsAllowedQueryColumn(key)))
                {
                    error = "One or more dynamic filter columns are not allowed.";
                    return false;
                }

                if (dictionary.Values.Any(values => values?.Take(MaxListSize + 1).Count() > MaxListSize))
                {
                    error = $"A filter value list cannot exceed {MaxListSize} items.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool HasRowVersion(T entity)
            => entity.RowVersion is { Length: > 0 };

        private async Task<Result<bool>> ConflictOrNotFoundAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            TKey id,
            CancellationToken cancellationToken)
        {
            var parameters = WithTenantParameters(new { id });
            var exists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                $"SELECT 1 FROM {tableNameSql} WHERE [CompanyId] = @__TenantCompanyId AND [Id] = @id;",
                parameters,
                transaction,
                cancellationToken: cancellationToken));
            return exists.HasValue
                ? Result<bool>.Fail(["The record changed after it was loaded. Reload and try again."], 409)
                : Result<bool>.Fail(["The record was not found."], 404);
        }

        private void LogFailure(Exception exception, string message, params object[] values)
            => logger.LogError(exception, message, values);

        // ----------------- Logging Helper -----------------
        private void LogSql(string sql, DynamicParameters? dp)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                logger.LogDebug("SQL: {Sql}", sql);
                if (dp != null)
                {
                    var names = dp.ParameterNames;
                    logger.LogDebug("Params: {Names}", string.Join(", ", names));
                }
                sw.Stop();
            }
            catch
            {
                // لا شيء
            }
        }
    }
}
