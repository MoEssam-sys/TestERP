using Dapper;
using Microsoft.Extensions.Logging;
using One.Domain.Services.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace One.Application.Base
{
    internal static class AttributeCrudEngine
    {
        // ---------- Caching ----------
        private sealed class PropMeta
        {
            public PropertyInfo Prop { get; init; } = default!;
            public string Column { get; init; } = default!;
            public InsertFieldAttribute? InsertAttr { get; init; }
            public UpdateFieldAttribute? UpdateAttr { get; init; }
            public bool IsDeleteKey { get; init; }
            public bool IsActiveKey { get; init; }
            public bool IsConcurrency { get; init; }
        }

        private sealed class TypeMeta
        {
            public string Table { get; init; } = default!;
            public PropertyInfo? IdProp { get; init; }
            public string? IdColumn { get; init; }
            public PropertyInfo? ActiveStateProp { get; init; } // "IsActive" إن وُجدت
            public PropertyInfo? ConcurrencyProp { get; init; }
            public IReadOnlyList<PropMeta> Props { get; init; } = default!;
            public Dictionary<string, PropMeta> ByName { get; init; } = default!;
        }

        private static readonly ConcurrentDictionary<Type, TypeMeta> _metaCache = new();

        internal static string ResolveTableName(Type t)
        {
            var table = t.GetCustomAttribute<DbTableAttribute>()?.Name;
            return string.IsNullOrWhiteSpace(table) ? t.Name : table!;
        }

        private static string ResolveColumnName(PropertyInfo p)
        {
            var col = p.GetCustomAttribute<DbColumnAttribute>()?.Name;
            return string.IsNullOrWhiteSpace(col) ? p.Name : col!;
        }

        private static bool IsReadable(PropertyInfo p) => p.CanRead && p.GetMethod?.IsPublic == true;

        private static bool IsWritable(PropertyInfo p) => p.CanWrite && p.SetMethod?.IsPublic == true;

        private static bool IsId(PropertyInfo p) => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase);

        private static bool IsActiveState(PropertyInfo p) => string.Equals(p.Name, "IsActive", StringComparison.OrdinalIgnoreCase);

        private static TypeMeta GetMeta(Type t)
        {
            return _metaCache.GetOrAdd(t, static (type) =>
            {
                var table = ResolveTableName(type);
                var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                .Where(IsReadable)
                                .Select(p => new PropMeta
                                {
                                    Prop = p,
                                    Column = ResolveColumnName(p),
                                    InsertAttr = p.GetCustomAttributes<InsertFieldAttribute>(true).LastOrDefault(),
                                    UpdateAttr = p.GetCustomAttributes<UpdateFieldAttribute>(true).LastOrDefault(),
                                    IsDeleteKey = p.GetCustomAttributes<DeleteFieldAttribute>(true).Any(),
                                    IsActiveKey = p.GetCustomAttributes<ActiveFieldAttribute>(true).Any(),
                                    IsConcurrency = p.GetCustomAttributes<ConcurrencyFieldAttribute>(true).Any(),
                                })
                                .ToList();

                var idProp = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                 .FirstOrDefault(IsId);
                var idCol = idProp != null ? ResolveColumnName(idProp) : null;

                var activeProp = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                     .FirstOrDefault(IsActiveState);

                return new TypeMeta
                {
                    Table = table,
                    Props = props,
                    ByName = props.ToDictionary(x => x.Prop.Name, x => x, StringComparer.OrdinalIgnoreCase),
                    IdProp = idProp,
                    IdColumn = idCol,
                    ActiveStateProp = activeProp,
                    ConcurrencyProp = props.FirstOrDefault(x => x.IsConcurrency)?.Prop
                };
            });
        }

        // ---------- Validation ----------
        internal static List<string> ValidateRequired(object entity, bool isInsert)
        {
            var errors = new List<string>();
            var meta = GetMeta(entity.GetType());

            foreach (var p in meta.Props)
            {
                // خد الفلاج مباشرة بدل ما ترجع Attribute بأنواع مختلفة
                bool requiredFlag = isInsert
                    ? (p.InsertAttr?.Required ?? false)
                    : (p.UpdateAttr?.Required ?? false);

                if (!requiredFlag) continue;

                var val = p.Prop.GetValue(entity);
                if (!IsValidRequiredValue(val, p.Prop.PropertyType))
                {
                    errors.Add($"Field '{p.Prop.Name}' is required.");
                }
            }

            return errors;
        }

        private static bool IsValidRequiredValue(object? val, Type type)
        {
            if (val == null) return false;

            var t = Nullable.GetUnderlyingType(type) ?? type;

            if (t == typeof(string))
                return !string.IsNullOrWhiteSpace((string)val);

            if (t == typeof(Guid))
                return (Guid)val != Guid.Empty;

            // Numeric zero can be a valid business value. Numeric ranges belong
            // to request validation or a dedicated domain service.
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) ||
                t == typeof(byte) || t == typeof(float) || t == typeof(double) ||
                t == typeof(decimal))
            {
                return true;
            }

            // bool/date/others: يكفي عدم null (القيمة موجودة)
            return true;
        }

        // ---------- Insert ----------
        internal static (string sql, DynamicParameters dp, TKey idValue) BuildInsertSql<TKey>(object entity)
        {
            var meta = GetMeta(entity.GetType());
            var table = meta.Table;

            var cols = new List<string>();
            var vals = new List<string>();
            var dp = new DynamicParameters();

            foreach (var p in meta.Props)
            {
                var a = p.InsertAttr;
                if (a == null) continue;

                var val = p.Prop.GetValue(entity);
                val = TrimOrNullIfEmpty(val, a.SkipIfNull);
                if (val == null && a.SkipIfNull) continue;

                var paramName = $"@{p.Prop.Name}";
                cols.Add($"[{p.Column}]");
                vals.Add(paramName);
                dp.Add(paramName, val);
            }

            if (cols.Count == 0)
                throw new InvalidOperationException("No insertable fields found. Add [InsertField] attributes.");

            // نرجّح وجود عمود Id لإخراجه — OUTPUT INSERTED.Id
            string outputSql = "";
            string idColumn = meta.IdColumn ?? "Id";
            var hasId = meta.IdProp != null;
            if (hasId)
            {
                outputSql = $" OUTPUT INSERTED.[{idColumn}]";
            }

            var sb = new StringBuilder();
            sb.Append($"INSERT INTO [{table}] (");
            sb.Append(string.Join(",", cols));
            sb.Append(")");
            sb.Append(outputSql);
            sb.Append(" VALUES (");
            sb.Append(string.Join(",", vals));
            sb.Append(");");

            // placeholder id value (default)
            var idVal = default(TKey)!;
            return (sb.ToString(), dp, idVal);
        }

        // ---------- Update ----------
        internal static (string sql, DynamicParameters dp) BuildUpdateSql(object entity)
        {
            var meta = GetMeta(entity.GetType());
            var table = meta.Table;

            var sets = new List<string>();
            var where = new List<string>();
            var dp = new DynamicParameters();

            // SET
            foreach (var p in meta.Props)
            {
                var a = p.UpdateAttr;
                if (a == null) continue;

                var val = p.Prop.GetValue(entity);
                val = TrimOrNullIfEmpty(val, a.SkipIfNull);
                if (val == null && a.SkipIfNull) continue;

                var param = $"@{p.Prop.Name}";
                sets.Add($"[{p.Column}] = {param}");
                dp.Add(param, val);
            }

            if (sets.Count == 0)
                throw new InvalidOperationException("No updatable fields found. Add [UpdateField] attributes.");

            // WHERE: أولوية Id لو موجود ومحدد؛ وإلا استخدم DeleteField/ActiveField كمفاتيح
            if (meta.IdProp != null)
            {
                var idVal = meta.IdProp.GetValue(entity);
                if (idVal != null && !IsDefaultOrEmpty(idVal))
                {
                    var idParam = "@_Id";
                    where.Add($"[{meta.IdColumn}] = {idParam}");
                    dp.Add(idParam, idVal);
                }
            }

            if (where.Count == 0)
            {
                var keys = meta.Props.Where(x => x.IsDeleteKey || x.IsActiveKey).ToList();
                if (keys.Count == 0)
                    throw new InvalidOperationException("No WHERE keys found for UPDATE. Provide Id or mark keys with [DeleteField]/[ActiveField].");

                foreach (var k in keys)
                {
                    var val = k.Prop.GetValue(entity);
                    val = TrimIfString(val);
                    if (IsDefaultOrEmpty(val))
                        throw new InvalidOperationException($"Key '{k.Prop.Name}' is empty for UPDATE.");

                    var param = $"@_{k.Prop.Name}";
                    where.Add($"[{k.Column}] = {param}");
                    dp.Add(param, val);
                }
            }

            AddConcurrencyPredicate(meta, entity, where, dp);

            var sql = $"UPDATE [{table}] SET {string.Join(", ", sets)} WHERE {string.Join(" AND ", where)};";
            return (sql, dp);
        }

        // ---------- Activate/Deactivate ----------
        internal static (string sql, DynamicParameters dp) BuildSetActiveSql(object entity, bool isActive, string? stateColumnOverride = null)
        {
            var meta = GetMeta(entity.GetType());
            var table = meta.Table;

            var where = new List<string>();
            var dp = new DynamicParameters();

            var keys = meta.Props.Where(x => x.IsActiveKey || x.IsDeleteKey).ToList();
            if (keys.Count == 0 && meta.IdProp != null)
            {
                var idVal = meta.IdProp.GetValue(entity);
                if (IsDefaultOrEmpty(idVal))
                    throw new InvalidOperationException("Id is required for SetActive when no [ActiveField]/[DeleteField] present.");

                var idParam = "@_Id";
                where.Add($"[{meta.IdColumn}] = {idParam}");
                dp.Add(idParam, idVal);
            }
            else
            {
                foreach (var k in keys)
                {
                    var val = k.Prop.GetValue(entity);
                    val = TrimIfString(val);
                    if (IsDefaultOrEmpty(val))
                        throw new InvalidOperationException($"Active key '{k.Prop.Name}' is empty.");

                    var param = $"@_{k.Prop.Name}";
                    where.Add($"[{k.Column}] = {param}");
                    dp.Add(param, val);
                }
            }

            // اسم عمود الحالة
            var stateCol = !string.IsNullOrWhiteSpace(stateColumnOverride)
                           ? stateColumnOverride!
                           : meta.ActiveStateProp != null ? ResolveColumnName(meta.ActiveStateProp) : "IsActive";

            AddConcurrencyPredicate(meta, entity, where, dp);

            var sets = new List<string> { $"[{stateCol}] = @__IsActive" };
            foreach (var auditName in new[] { "UpdatedAt", "UpdatedBy" })
            {
                if (!meta.ByName.TryGetValue(auditName, out var audit))
                    continue;

                var value = audit.Prop.GetValue(entity);
                sets.Add($"[{audit.Column}] = @__{auditName}");
                dp.Add($"@__{auditName}", value);
            }

            var sql = $"UPDATE [{table}] SET {string.Join(", ", sets)} WHERE {string.Join(" AND ", where)};";
            dp.Add("@__IsActive", isActive);

            return (sql, dp);
        }

        // ---------- Select by Id / All ----------
        internal static (string sql, DynamicParameters dp) BuildGetByIdSql<T, TKey>(TKey id)
        {
            var meta = GetMeta(typeof(T));
            var table = meta.Table;

            var idCol = meta.IdColumn ?? "Id";
            var dp = new DynamicParameters();
            dp.Add("@_Id", id);

            var sql = $"SELECT * FROM [{table}] WHERE [{idCol}] = @_Id;";
            return (sql, dp);
        }

        internal static string BuildGetAllSql<T>()
        {
            var meta = GetMeta(typeof(T));
            return $"SELECT * FROM [{meta.Table}];";
        }

        // ---------- Select by Filter ----------
        internal static (string sql, DynamicParameters dp) BuildSelectByFilterSql(object filter)
        {
            var t = filter.GetType();
            var table = ResolveTableName(t); // DbTable على الفلتر نفسه
            if (string.IsNullOrWhiteSpace(table))
                throw new InvalidOperationException("Filter class must have [DbTable] to target table name.");

            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(IsReadable)
                         .ToList();

            var dp = new DynamicParameters();
            var conds = new List<string>();
            int idx = 0;

            // MatchOn
            foreach (var p in props)
            {
                foreach (var m in p.GetCustomAttributes<MatchOnAttribute>(true))
                {
                    if ((m.AppliesTo & Operation.Select) == 0) continue;

                    var val = p.GetValue(filter);
                    if (val == null && m.SkipIfNull) continue;

                    if (string.Equals(m.Operator, "BETWEEN", StringComparison.OrdinalIgnoreCase))
                    {
                        var toName = m.RangeToProperty;
                        if (string.IsNullOrWhiteSpace(toName))
                            continue;

                        var toProp = props.FirstOrDefault(x => x.Name.Equals(toName, StringComparison.Ordinal));
                        if (toProp == null) continue;

                        var toVal = toProp.GetValue(filter);
                        toVal = TrimOrNullIfEmpty(toVal, m.SkipIfNull);
                        if (val == null || toVal == null) continue;

                        var p1 = $"@p{idx++}";
                        var p2 = $"@p{idx++}";
                        var col = ResolveColumnName(p);

                        conds.Add($"[{col}] BETWEEN {p1} AND {p2}");
                        dp.Add(p1, val);
                        dp.Add(p2, toVal);
                    }
                    else
                    {
                        if (!AllowedOperators.Contains(m.Operator))
                            throw new InvalidOperationException($"Unsupported filter operator '{m.Operator}'.");

                        var pn = $"@p{idx++}";
                        var col = ResolveColumnName(p);

                        if (string.Equals(m.Operator, "LIKE", StringComparison.OrdinalIgnoreCase) && val is string s)
                        {
                            dp.Add(pn, s.Contains('%') ? s : $"%{s}%");
                        }
                        else
                        {
                            dp.Add(pn, val);
                        }

                        conds.Add($"[{col}] {m.Operator} {pn}");
                    }
                }
            }

            // MatchDictionary (IN conditions على أعمدة مختلفة)
            var dictProp = props.FirstOrDefault(x =>
                x.GetCustomAttributes<MatchDictionaryAttribute>(true)
                 .Any(md => (md.AppliesTo & Operation.Select) != 0));

            if (dictProp != null)
            {
                var md = dictProp.GetCustomAttributes<MatchDictionaryAttribute>(true)
                                 .First(x => (x.AppliesTo & Operation.Select) != 0);

                var dict = dictProp.GetValue(filter) as Dictionary<string, IEnumerable<object>>;
                if (dict != null && dict.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var kv in dict)
                    {
                        // حماية من SQL Injection: اسم العمود حروف/أرقام/Underscore فقط
                        if (!Regex.IsMatch(kv.Key, @"^[A-Za-z0-9_]+$")) continue;

                        // 🔽 تنظف القيم: Trim لو string، واشطب الفاضي
                        var cleaned = kv.Value?
                            .Select(v => TrimIfString(v))
                            .Where(v => v != null && (v is not string ss || ss.Length > 0))
                            .ToList();

                        if (cleaned == null || cleaned.Count == 0) continue;

                        var pn = $"@in{idx++}";
                        parts.Add($"[{kv.Key}] IN {pn}");
                        dp.Add(pn, cleaned);
                    }

                    if (parts.Count > 0)
                    {
                        var joiner = md.CombineWithAnd ? " AND " : " OR ";
                        conds.Add("(" + string.Join(joiner, parts) + ")");
                    }
                }
            }

            var sb = new StringBuilder();
            sb.Append($"SELECT * FROM [{table}]");
            if (conds.Count > 0)
            {
                sb.Append(" WHERE ");
                sb.Append(string.Join(" AND ", conds));
            }
            sb.Append(";");

            return (sb.ToString(), dp);
        }

        // ---------- Helpers ----------
        private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
        {
            "=", "<>", ">", ">=", "<", "<=", "LIKE"
        };

        private static void AddConcurrencyPredicate(
            TypeMeta meta,
            object entity,
            ICollection<string> where,
            DynamicParameters parameters)
        {
            if (meta.ConcurrencyProp is null)
                throw new InvalidOperationException("Reference-data entities require a concurrency field.");

            var value = meta.ConcurrencyProp.GetValue(entity) as byte[];
            if (value is not { Length: > 0 })
                throw new InvalidOperationException("A row version is required.");

            var column = ResolveColumnName(meta.ConcurrencyProp);
            where.Add($"[{column}] = @__OriginalRowVersion");
            parameters.Add("@__OriginalRowVersion", value, DbType.Binary);
        }

        private static bool IsDefaultOrEmpty(object? val)
        {
            if (val == null) return true;
            var t = Nullable.GetUnderlyingType(val.GetType()) ?? val.GetType();

            if (t == typeof(Guid)) return (Guid)val == Guid.Empty;
            if (t == typeof(int)) return (int)val == 0;
            if (t == typeof(long)) return (long)val == 0L;
            if (t == typeof(short)) return (short)val == 0;
            if (t == typeof(byte)) return (byte)val == 0;
            if (t == typeof(float)) return Math.Abs((float)val) < float.Epsilon;
            if (t == typeof(double)) return Math.Abs((double)val) < double.Epsilon;
            if (t == typeof(decimal)) return (decimal)val == 0m;
            if (t == typeof(string)) return string.IsNullOrWhiteSpace((string)val);

            return false;
        }

        private static object? TrimIfString(object? v)
        {
            if (v is string s) return s.Trim();
            return v;
        }

        private static object? TrimOrNullIfEmpty(object? v, bool treatEmptyAsNull)
        {
            var x = TrimIfString(v);
            if (treatEmptyAsNull && x is string ss && ss.Length == 0)
                return null;
            return x;
        }
    }
}
