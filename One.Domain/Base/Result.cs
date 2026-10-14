namespace One.Domain.Base
{
    public class Result<T>
    {
        public bool Success { get; set; }
        public List<string?>? Messages { get; set; }
        public T? Data { get; set; }
        public int? StatusCode { get; set; }
        public string? ErrorId { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public bool IsWarning { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;


        // ======= Static Methods =======

        /// <summary>
        /// نجاح عادي (200 OK)
        /// <code>
        /// return Result&lt;Product&gt;.Ok(product);
        /// </code>
        /// </summary>
        public static Result<T> Ok(T data, List<string?>? msgs = null, Dictionary<string, object>? meta = null)
            => new()
            {
                Success = true,
                Data = data,
                StatusCode = 200,
                Messages = msgs,
                Metadata = meta
            };

        /// <summary>
        /// نجاح مع تحذير (200 OK + Warning)
        /// <code>
        /// return Result&lt;Product&gt;.Warning(product, new List&lt;string?&gt; { "تم الحفظ ولكن بعض الحقول لم تُحفظ." });
        /// </code>
        /// </summary>
        public static Result<T> Warning(T data, List<string?> msgs, Dictionary<string, object>? meta = null)
            => new()
            {
                Success = true,
                Data = data,
                IsWarning = true,
                StatusCode = 200,
                Messages = msgs,
                Metadata = meta
            };

        /// <summary>
        /// فشل عادي (400 Bad Request)
        /// <code>
        /// return Result&lt;Product&gt;.Fail(new List&lt;string?&gt; { "المنتج غير موجود." });
        /// </code>
        /// </summary>
        public static Result<T> Fail(List<string?> msgs, int statusCode = 400, string? errorId = null)
            => new()
            {
                Success = false,
                Messages = msgs,
                StatusCode = statusCode,
                ErrorId = errorId ?? Guid.NewGuid().ToString(),
            };

        /// <summary>
        /// فشل داخلي مع ErrorId تلقائي (500 Internal Error)
        /// <code>
        /// try
        /// {
        ///     // ...
        /// }
        /// catch (Exception ex)
        /// {
        ///     return Result&lt;Product&gt;.InternalError(ex);
        /// }
        /// </code>
        /// </summary>
        public static Result<T> InternalError(Exception ex, string? msg = null)
            => new()
            {
                Success = false,
                Messages = new List<string?> { msg ?? "حدث خطأ غير متوقع. الرجاء المحاولة لاحقًا." },
                StatusCode = 500,
                ErrorId = Guid.NewGuid().ToString(),
            };

        /// <summary>
        /// فشل مع بيانات مرفقة (400 Bad Request)
        /// <code>
        /// return Result&lt;Product&gt;.FailWithData(new List&lt;string?&gt; { "خطأ في حفظ البيانات" }, product);
        /// </code>
        /// </summary>
        public static Result<T> FailWithData(List<string?> msgs, T? data, int statusCode = 400, string? errorId = null)
            => new()
            {
                Success = false,
                Data = data,
                Messages = msgs,
                StatusCode = statusCode,
                ErrorId = errorId ?? Guid.NewGuid().ToString(),
            };

        // ===================================

        //١. مثال على نوع بيانات آخر(Category) :

        /// <summary>
        /// نجاح عادي مع نوع بيانات مختلف (Category)
        /// <code>
        /// var category = new Category { Id = 1, Name = "أدوية" };
        /// return Result&lt;Category&gt;.Ok(category);
        /// </code>
        /// </summary>
        ///

        //٢. مثال Metadata(Pagination):

        /// <summary>
        /// نجاح مع بيانات إضافية (Metadata) مثل بيانات الصفحة (Pagination)
        /// <code>
        /// var products = new List&lt;Product&gt; { ... };
        /// var meta = new Dictionary&lt;string, object&gt; {
        ///     { "TotalCount", 120 },
        ///     { "PageNumber", 2 },
        ///     { "PageSize", 20 }
        /// };
        /// return Result&lt;List&lt;Product&gt;&gt;.Ok(products, meta: meta);
        /// </code>
        /// </summary>
        ///

        //٣. مثال Warning مع Metadata:

        /// <summary>
        /// نجاح بتحذير، مع بيانات إضافية (Metadata)
        /// <code>
        /// var meta = new Dictionary&lt;string, object&gt; {
        ///     { "ProcessedCount", 3 }
        /// };
        /// return Result&lt;Category&gt;.Warning(category, new List&lt;string?&gt; { "تمت العملية مع بعض التحذيرات." }, meta);
        /// </code>
        /// </summary>
        ///

        //٤. مثال ErrorId في فشل داخلي:

        /// <summary>
        /// فشل داخلي مع ErrorId تلقائي لسهولة التتبع في اللوج أو الدعم
        /// <code>
        /// try
        /// {
        ///     // عملية خطرة أو معقدة
        /// }
        /// catch (Exception ex)
        /// {
        ///     return Result&lt;Category&gt;.InternalError(ex, "فشل في تحديث التصنيف");
        /// }
        /// </code>
        /// </summary>
        ///

        //تلخيص — تقدر تستخدمها هكذا:

        //// Category Example
        //var category = new Category { Id = 1, Name = "فيتامينات" };
        //return Result<Category>.Ok(category);

        //// Metadata Example (Pagination)
        //var meta = new Dictionary<string, object> {
        //    { "TotalCount", 1200 },
        //    { "Page", 2 },
        //    { "PageSize", 20 }
        //};
        //return Result<List<Product>>.Ok(products, meta: meta);

        //// Warning Example
        //return Result<Category>.Warning(category, new List<string?> { "هذا التصنيف غير نشط." }, meta);
    }
}
