using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using One.Shared.Services.Logging;

namespace One.Shared.Services.ErrorNotifier
{
    public interface IGlobalErrorNotifier
    {
        Task NotifyAsync(string title, string? details = null);

        Task NotifyExceptionAsync(string title, Exception ex);
    }

    /// <summary>
    /// Notifier عام للطبقات المشتركة:
    /// - لو MessagesCenter متسجّل في DI (في مشروع MAUI) → يعرض الرسائل عبر Snackbar/Dialog.
    /// - وإلا يعمل Fallback لطيف: Debug.WriteLine بدون أي اعتماد على MAUI.
    /// </summary>
    public sealed class GlobalErrorNotifier : IGlobalErrorNotifier
    {
        private readonly IServiceProvider _sp;

        public GlobalErrorNotifier(IServiceProvider sp) => _sp = sp;

        public async Task NotifyAsync(string title, string? details = null)
        {
            var msg = _sp.GetService<MessagesCenter>();

            // جهّز الرسائل
            var messages = new List<string?>();
            if (!string.IsNullOrWhiteSpace(title)) messages.Add(title);
            if (!string.IsNullOrWhiteSpace(details)) messages.Add(details);
            messages = messages.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            if (msg is not null)
            {
                // عرض عبر Snackbar/Dialog
                await msg.Error(messages);
                return;
            }

            // Fallback: لا تعتمد على MAUI—سجّل فقط
            WriteDebug("ERROR", messages);
        }

        public async Task NotifyExceptionAsync(string title, Exception ex)
        {
            var msg = _sp.GetService<MessagesCenter>();
            var logger = _sp.GetService<IClientLogger>();

            // Log it
            if (logger != null)
            {
                await logger.LogError(title, action: "GlobalError", ex: ex);
            }
            else
            {
                // Fallback debug
                WriteDebug("EXCEPTION", new List<string?> { title, ex.Message, ex.StackTrace });
            }

            if (msg is not null)
            {
                // هيعرض رسالة ودّية + تفاصيل في DEBUG حسب تنفيذ MessagesCenter
                await msg.ExceptionHandle(title, ex);
            }
        }

        private static void WriteDebug(string prefix, IEnumerable<string?> lines)
        {
            try
            {
                var text = string.Join("\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
                Debug.WriteLine($"[{prefix}] {text}");
            }
            catch
            {
                // تجاهل أي أخطاء في الـdebug logging
            }
        }
    }
}
