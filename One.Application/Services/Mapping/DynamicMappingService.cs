using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using One.Domain.Services.Attributes;
using System.Collections.Concurrent;
using System.Reflection;

namespace One.Application.Services.Mapping
{
    public sealed class DynamicMappingService : IDynamicMappingService
    {
        private readonly ILogger _logger;

        public DynamicMappingService(ILogger<DynamicMappingService>? logger = null)
        {
            _logger = logger ?? NullLogger<DynamicMappingService>.Instance;
        }

        // ===== Plans Cache (لتحسين الأداء) =====
        private enum MapOp
        { Create, Update, Delete, Active }

        private sealed record PlanKey(Type Request, Type Entity, MapOp Op);

        private sealed class Plan
        {
            public List<Action<object, object>> Assigners { get; } = new();

            public override string ToString() => $"Assigners={Assigners.Count}";
        }

        private static readonly ConcurrentDictionary<PlanKey, Plan> _plans = new();

        public TEntity MapCreate<TEntity, TRequest>(TRequest source) where TEntity : new()
            => (TEntity)MapInternal(typeof(TRequest), typeof(TEntity), MapOp.Create, source!);

        public TEntity MapUpdate<TEntity, TRequest>(TRequest source) where TEntity : new()
            => (TEntity)MapInternal(typeof(TRequest), typeof(TEntity), MapOp.Update, source!);

        public TEntity MapDelete<TEntity, TRequest>(TRequest source) where TEntity : new()
            => (TEntity)MapInternal(typeof(TRequest), typeof(TEntity), MapOp.Delete, source!);

        public TEntity MapActive<TEntity, TRequest>(TRequest source) where TEntity : new()
            => (TEntity)MapInternal(typeof(TRequest), typeof(TEntity), MapOp.Active, source!);

        private object MapInternal(Type reqType, Type entType, MapOp op, object source)
        {
            var plan = _plans.GetOrAdd(new PlanKey(reqType, entType, op), k => BuildPlan(k));

            var entity = Activator.CreateInstance(entType)!;
            foreach (var assign in plan.Assigners)
                assign(source, entity);

            _logger.LogDebug("Dynamic map {Req}->{Ent} ({Op}): {Plan}", reqType.Name, entType.Name, op, plan);
            return entity;
        }

        // ===== Plan Builder =====
        private static Plan BuildPlan(PlanKey key)
        {
            var plan = new Plan();

            var reqProps = key.Request.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                      .Where(p => p.CanRead).ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            var entProps = key.Entity.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                     .Where(p => p.CanWrite && p.CanRead)
                                     .ToArray();

            switch (key.Op)
            {
                case MapOp.Create:
                    {
                        // خُد فقط الأعمدة اللي عليها [InsertField]
                        foreach (var ep in entProps)
                        {
                            var insAttr = ep.GetCustomAttributes<InsertFieldAttribute>(true).LastOrDefault();
                            if (insAttr is null) continue;

                            if (TryFindRequestProp(reqProps, ep, out var rp))
                                plan.Assigners.Add(MakeAssigner(rp, ep));
                        }
                        // Id لا ينسخ في create (الداتابيز بتولده)
                        break;
                    }

                case MapOp.Update:
                    {
                        // Id (لو موجود في الـ request) + الأعمدة اللي عليها [UpdateField]
                        // 1) Id
                        if (reqProps.TryGetValue("Id", out var rpId))
                        {
                            var epId = entProps.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase));
                            if (epId != null) plan.Assigners.Add(MakeAssigner(rpId, epId));
                        }

                        // 2) بقية الحقول
                        foreach (var ep in entProps)
                        {
                            var updAttr = ep.GetCustomAttributes<UpdateFieldAttribute>(true).LastOrDefault();
                            if (updAttr is null) continue;

                            if (TryFindRequestProp(reqProps, ep, out var rp))
                                plan.Assigners.Add(MakeAssigner(rp, ep));
                        }
                        AddConcurrencyAssigner(reqProps, entProps, plan);
                        break;
                    }

                case MapOp.Delete:
                    {
                        // لو فيه [DeleteField] على كيانات، استخدمها؛ غير كده Id
                        var deleteKeys = entProps.Where(p => p.GetCustomAttributes<DeleteFieldAttribute>(true).Any()).ToList();
                        if (deleteKeys.Count == 0)
                        {
                            var epId = entProps.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
                                       ?? throw new InvalidOperationException("Delete mapping: no [DeleteField] keys and no Id property on entity.");
                            if (!reqProps.TryGetValue("Id", out var rpId))
                                throw new InvalidOperationException("Delete mapping: request must contain 'Id'.");

                            plan.Assigners.Add(MakeAssigner(rpId, epId));
                        }
                        else
                        {
                            foreach (var ep in deleteKeys)
                            {
                                if (!TryFindRequestProp(reqProps, ep, out var rp))
                                    throw new InvalidOperationException($"Delete mapping: request missing key '{ep.Name}'.");
                                plan.Assigners.Add(MakeAssigner(rp, ep));
                            }
                        }
                        break;
                    }

                case MapOp.Active:
                    {
                        // مفاتيح: [ActiveField] أو [DeleteField] أو Id
                        var activeKeys = entProps.Where(p => p.GetCustomAttributes<ActiveFieldAttribute>(true).Any()).ToList();
                        if (activeKeys.Count == 0)
                            activeKeys = entProps.Where(p => p.GetCustomAttributes<DeleteFieldAttribute>(true).Any()).ToList();

                        if (activeKeys.Count == 0)
                        {
                            var epId = entProps.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
                                       ?? throw new InvalidOperationException("Active mapping: no keys and no Id on entity.");
                            if (!reqProps.TryGetValue("Id", out var rpId))
                                throw new InvalidOperationException("Active mapping: request must contain 'Id'.");
                            plan.Assigners.Add(MakeAssigner(rpId, epId));
                        }
                        else
                        {
                            foreach (var ep in activeKeys)
                            {
                                if (!TryFindRequestProp(reqProps, ep, out var rp))
                                    throw new InvalidOperationException($"Active mapping: request missing key '{ep.Name}'.");
                                plan.Assigners.Add(MakeAssigner(rp, ep));
                            }
                        }

                        // حالة التفعيل نفسها (IsActive) إن وُجدت في الطرفين
                        var epActive = entProps.FirstOrDefault(p => string.Equals(p.Name, "IsActive", StringComparison.OrdinalIgnoreCase));
                        if (epActive != null && reqProps.TryGetValue("IsActive", out var rpActive))
                            plan.Assigners.Add(MakeAssigner(rpActive, epActive));

                        AddConcurrencyAssigner(reqProps, entProps, plan);

                        break;
                    }
            }

            return plan;
        }

        private static void AddConcurrencyAssigner(
            IReadOnlyDictionary<string, PropertyInfo> requestProperties,
            IEnumerable<PropertyInfo> entityProperties,
            Plan plan)
        {
            var entityProperty = entityProperties.FirstOrDefault(property =>
                property.GetCustomAttributes<ConcurrencyFieldAttribute>(true).Any());
            if (entityProperty is not null &&
                requestProperties.TryGetValue(entityProperty.Name, out var requestProperty))
            {
                plan.Assigners.Add(MakeAssigner(requestProperty, entityProperty));
            }
        }

        // ابحث عن خاصية بالاسم (case-insensitive). لو عندك MapFrom لاحقًا ممكن تتوسع هنا.
        private static bool TryFindRequestProp(
            Dictionary<string, PropertyInfo> reqProps,
            PropertyInfo entityProp,
            out PropertyInfo requestProp)
        {
            return reqProps.TryGetValue(entityProp.Name, out requestProp!);
        }

        // ===== Assigner Builder =====
        private static Action<object, object> MakeAssigner(PropertyInfo src, PropertyInfo dst)
        {
            var sType = src.PropertyType;
            var dType = dst.PropertyType;

            if (!CanAssign(sType, dType))
            {
                // هندعم تحويلات شائعة: Nullable unwrap, Guid <-> string, Enums, numeric
                return (s, d) =>
                {
                    var val = src.GetValue(s);
                    if (val == null)
                    {
                        dst.SetValue(d, null);
                        return;
                    }
                    var converted = TryConvertValue(val, dType);
                    dst.SetValue(d, converted);
                };
            }

            return (s, d) =>
            {
                var val = src.GetValue(s);
                dst.SetValue(d, val);
            };
        }

        private static bool CanAssign(Type from, Type to)
        {
            var uTo = Nullable.GetUnderlyingType(to) ?? to;
            var uFrom = Nullable.GetUnderlyingType(from) ?? from;
            return uTo.IsAssignableFrom(uFrom);
        }

        private static object? TryConvertValue(object value, Type destType)
        {
            var nonNullDest = Nullable.GetUnderlyingType(destType) ?? destType;

            // Guid
            if (nonNullDest == typeof(Guid))
            {
                if (value is Guid g) return g;
                if (value is string s && Guid.TryParse(s, out var g2)) return g2;
            }

            // Enum
            if (nonNullDest.IsEnum)
            {
                if (value is string es && Enum.TryParse(nonNullDest, es, true, out var ev)) return ev;
                if (IsNumber(value)) return Enum.ToObject(nonNullDest, value);
            }

            // Numbers
            try
            {
                if (nonNullDest == typeof(decimal)) return Convert.ToDecimal(value);
                if (nonNullDest == typeof(double)) return Convert.ToDouble(value);
                if (nonNullDest == typeof(float)) return Convert.ToSingle(value);
                if (nonNullDest == typeof(long)) return Convert.ToInt64(value);
                if (nonNullDest == typeof(int)) return Convert.ToInt32(value);
                if (nonNullDest == typeof(short)) return Convert.ToInt16(value);
                if (nonNullDest == typeof(byte)) return Convert.ToByte(value);
            }
            catch { /* ignore */ }

            // DateTime
            if (nonNullDest == typeof(DateTime))
            {
                if (value is DateTime dt) return dt;
                if (value is string ds && DateTime.TryParse(ds, out var dt2)) return dt2;
            }

            // string
            if (nonNullDest == typeof(string))
                return value?.ToString();

            // fallback
            return value;
        }

        private static bool IsNumber(object v)
        {
            return v is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
        }
    }
}
