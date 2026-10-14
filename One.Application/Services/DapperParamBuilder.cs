using Dapper;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

namespace One.Application.Services
{
    public static class DapperParamBuilder
    {
        // Cache for stored procedure parameters
        public sealed record ProcParam(string Name, ParameterDirection Direction);
        private static readonly ConcurrentDictionary<string, List<ProcParam>> _procParamsCache = new();

        // Cache for property metadata per type (performance optimization)
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();
        private static readonly ConcurrentDictionary<PropertyInfo, (DbType dbType, int size)> _dbTypeCache = new();

        public static DynamicParameters Build<T>(
            IDbConnection conn,
            string procName,
            T entity,
            IDbTransaction? tran = null,
            string[]? ignoreProps = null,
            bool addOutput = true)
        {
            var p = new DynamicParameters();
            ignoreProps ??= [];

            var meta = GetStoredProcParamsCached(conn, procName, tran);
            var allowedNames = meta.Select(m => m.Name)
                                   .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Use cached property metadata for better performance
            var properties = GetCachedProperties<T>();

            foreach (var prop in properties)
            {
                if (!prop.CanRead || ignoreProps.Contains(prop.Name)) continue;
                if (!allowedNames.Contains(prop.Name)) continue;

                var value = prop.GetValue(entity);
                var (dbType, size) = GetDbTypeAndSizeCached(prop);

                if (dbType == DbType.String)
                {
                    value ??= "";
                    if (size > 0) p.Add("@" + prop.Name, value, dbType, size: size);
                    else p.Add("@" + prop.Name, value, dbType);
                }
                else if (value == null)
                {
                    p.Add("@" + prop.Name, DBNull.Value, dbType);
                }
                else
                {
                    p.Add("@" + prop.Name, value, dbType);
                }
            }

            // Add OUTPUT parameters only if the stored procedure defines them
            if (addOutput)
            {
                static bool IsOut(ProcParam x)
                    => x.Direction is ParameterDirection.Output or ParameterDirection.InputOutput;

                if (meta.Any(x => x.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) && IsOut(x)))
                    p.Add("@Id", dbType: DbType.Guid, direction: ParameterDirection.Output);

                if (meta.Any(x => x.Name.Equals("Success", StringComparison.OrdinalIgnoreCase) && IsOut(x)))
                    p.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                if (meta.Any(x => x.Name.Equals("Message", StringComparison.OrdinalIgnoreCase) && IsOut(x)))
                    p.Add("@Message", dbType: DbType.String, size: 1000, direction: ParameterDirection.Output);
            }

            return p;
        }

        public static bool TryAddOptionalParam(
            IDbConnection conn, string procName, DynamicParameters p,
            string paramName, object? value, IDbTransaction? tran = null)
        {
            var meta = GetStoredProcParamsCached(conn, procName, tran);
            if (meta.Any(m => m.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase)))
            {
                p.Add("@" + paramName, value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets cached properties for a type to avoid repeated reflection calls.
        /// </summary>
        private static PropertyInfo[] GetCachedProperties<T>()
        {
            return _propertyCache.GetOrAdd(typeof(T), t =>
                t.GetProperties(BindingFlags.Instance | BindingFlags.Public));
        }

        /// <summary>
        /// Gets cached DbType and size for a property to avoid repeated calculation.
        /// </summary>
        private static (DbType dbType, int size) GetDbTypeAndSizeCached(PropertyInfo prop)
        {
            return _dbTypeCache.GetOrAdd(prop, p => GetDbTypeAndSize(p));
        }

        private static IReadOnlyList<ProcParam> GetStoredProcParamsCached(
            IDbConnection conn, string procName, IDbTransaction? tran)
        {
            if (_procParamsCache.TryGetValue(procName, out var cached))
                return cached;

            var shouldClose = false;
            if (conn.State != ConnectionState.Open) { conn.Open(); shouldClose = true; }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = procName;
            cmd.CommandType = CommandType.StoredProcedure;
            if (tran != null) cmd.Transaction = (SqlTransaction)tran;

            SqlCommandBuilder.DeriveParameters((SqlCommand)cmd);

            var list = cmd.Parameters
                          .Cast<SqlParameter>()
                          .Select(p => new ProcParam(p.ParameterName.TrimStart('@'), p.Direction))
                          .ToList();

            _procParamsCache[procName] = list;
            if (shouldClose) conn.Close();
            return list;
        }

        private static (DbType dbType, int size) GetDbTypeAndSize(PropertyInfo prop)
        {
            var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var name = t.Name;

            if (name == "String")
            {
                int size = 255;
                var n = prop.Name.ToLower();
                if (n.Contains("name")) size = 200;
                if (n.Contains("description")) size = 500;
                if (n.Contains("barcode")) size = 100;
                if (n.Contains("message")) size = 1000;
                return (DbType.String, size);
            }

            return name switch
            {
                "Int32" => (DbType.Int32, 0),
                "Int64" => (DbType.Int64, 0),
                "Guid" => (DbType.Guid, 0),
                "Boolean" => (DbType.Boolean, 0),
                "Decimal" => (DbType.Decimal, 0),
                "Double" => (DbType.Double, 0),
                "DateTime" => (DbType.DateTime, 0),
                "Byte" => (DbType.Byte, 0),
                "Single" => (DbType.Single, 0),
                _ => (DbType.Object, 0)
            };
        }
    }
}