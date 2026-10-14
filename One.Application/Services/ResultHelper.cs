using Dapper;
using One.Domain.Base;
using One.Domain.Enums;

namespace One.Application.Services
{
    public static class ResultHelper
    {
        //Result.Fail سريع وواضح وموحد في الـ API والـ Controller.

        //FromOutput تخصصي — وظيفته قراءة نتائج الإجراءات المخزنة(Stored Procedures) ومهتم بالرسائل من الداتا بيز، وممكن يضيف ErrorId/Logging تلقائي.

        //ملاحظة مهمة:
        //لو يومًا نقلت عمليات الإدخال أو الحذف لتكون عبر Procedures (بدل SQL عادي)، عدل الدالة لتستخدم ResultHelper.FromOutput فورًا بدون ما تغير منطق المشروع كله.

        /// <summary>
        /// يبني نتيجة موحدة من Output Parameters بعد تنفيذ Stored Procedure
        /// - يولد ErrorId تلقائي عند أي فشل (ويرجعها في Result)
        /// - يسجل أي فشل في Logger (Console/DB/أي System تريده)
        /// <code>
        /// var res = ResultHelper.FromOutput<Product>(parameters, product, CrudOperation.Create, "Product");
        /// if (!res.Success)
        /// {
        ///     // تقدر ترسل ErrorId للمستخدم وتبلغه يحتفظ بيه
        ///     // أو تسجله في أي مكان داخلي
        /// }
        /// </code>
        /// </summary>
        public static Result<T> FromOutput<T>(
            DynamicParameters parameters,
            T? data = default,
            Operation operation = Operation.Other,
            string? entityName = null)
        {
            bool success = SafeGetOutput<bool>(parameters, "@Success");
            string? message = SafeGetOutput<string>(parameters, "@Message");

            string opStr = operation switch
            {
                Operation.Create => "creating",
                Operation.GetAll => "Get",
                Operation.GetById => "Get by Id",
                Operation.Update => "updating",
                Operation.Delete => "deleting",
                Operation.Activate => "activate",
                Operation.Deactivate => "deActivate",
                _ => "Other"
            };

            string? userMsg = null;
            string? errorId = null;

            if (!success)
            {
                // توليد ErrorId عند الفشل (ممكن ترسله للمستخدم أو الدعم الفني)
                errorId = Guid.NewGuid().ToString();

#if DEBUG
                // رسالة كاملة للمطور فقط
                userMsg = $"{(entityName ?? "Entity")}: Error {opStr}: {message} [ErrorId: {errorId}]";
#else
                // في Release أظهر فقط رسالة الـ DB لو موجودة، أو رسالة عامة ومعها ErrorId
                userMsg = !string.IsNullOrWhiteSpace(message)
                    ? $"{(entityName ?? "Entity")}: {message} [ErrorId: {errorId}]"
                    : $"{(entityName ?? "Entity")}: Error {opStr}: Something went wrong. Please try again. [ErrorId: {errorId}]";
#endif
                // سجل الخطأ في اللوج
                LogFailure(errorId, entityName, opStr, message);
            }

            var msgs = !success
                ? new List<string?> { userMsg }
                : (!string.IsNullOrWhiteSpace(message) ? new List<string?> { message } : null);

            return new Result<T>
            {
                Success = success,
                Messages = msgs,
                Data = data,
                ErrorId = errorId // يظهر فقط عند الفشل
            };
        }

        private static TOut? SafeGetOutput<TOut>(DynamicParameters parameters, string name)
        {
            try
            {
                var val = parameters.Get<dynamic>(name);
                if (val is TOut correctType)
                    return correctType;
                if (typeof(TOut) == typeof(bool) && val != null)
                {
                    if (val is int i) return (TOut)(object)(i != 0);
                    if (val is string s && bool.TryParse(s, out var b)) return (TOut)(object)b;
                }
                if (val == null) return default;
                return (TOut)Convert.ChangeType(val, typeof(TOut));
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// دالة تسجيل الخطأ، يمكنك ربطها بأي Logger تريده (Serilog, DB, ...).
        /// </summary>
        private static void LogFailure(string? errorId, string? entityName, string operation, string? message)
        {
            // هنا تقدر تستخدم أي Logger حقيقي: Serilog, NLog, Seq, أو حتى قاعدة بيانات أو ملف نصي
            // Console.Error.WriteLine مجرد مثال مؤقت

            Console.Error.WriteLine(
                $"[ERROR] [{DateTime.UtcNow}] [ErrorId: {errorId}] [{entityName}] [{operation}] Message: {message ?? "No message"}"
            );

            // مثال على توصيلها بـ Serilog (لو مسجل Serilog في مشروعك)
            // Log.Error("DB ErrorId: {ErrorId} | Entity: {Entity} | Operation: {Op} | Message: {Msg}", errorId, entityName, operation, message);
        }
    }
}