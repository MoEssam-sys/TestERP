using MudBlazor;
using One.Domain.Localization;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using One.Shared.OneComponents;
using One.Shared.Services.Logging;

namespace One.Shared.Services
{
    public class MessagesCenter
    {
        private readonly ISnackbar _snack;
        private readonly IDialogService _dialog;
        private readonly ITranslator _translator;
        private readonly IClientLogger _logger;

        public MessagesCenter(ISnackbar snack, IDialogService dialog, ITranslator translator, IClientLogger logger)
        {
            _snack = snack;
            _dialog = dialog;
            _translator = translator;
            _logger = logger;
        }

        string Tr(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var t = _translator[value];
            return string.IsNullOrWhiteSpace(t) ? value : t;
        }

        static readonly TimeSpan DedupWindow = TimeSpan.FromSeconds(2);
        static readonly Dictionary<string, DateTime> _recent = new();

        public Task CreatedSuccessfully(string createdObject)
        {
            _snack.Add(Tr(createdObject) + " " + Tr("CreatedSuccessfully"), Severity.Success);
            _ = _logger.LogInfo(createdObject + " Created Successfully", action: "Create");
            return Task.CompletedTask;
        }

        public Task UpdatedSuccessfully(string updatedObject)
        {
            _snack.Add(Tr(updatedObject) + " " + Tr("UpdatedSuccessfully"), Severity.Success);
            _ = _logger.LogInfo(updatedObject + " Updated Successfully", action: "Update");
            return Task.CompletedTask;
        }

        public Task DeletedSuccessfully(string deletedObject)
        {
            _snack.Add(Tr(deletedObject) + " " + Tr("DeletedSuccessfully"), Severity.Success);
            _ = _logger.LogInfo(deletedObject + " Deleted Successfully", action: "Delete");
            return Task.CompletedTask;
        }

        public Task NotFound(string notFoundObject)
        {
            _snack.Add(Tr(notFoundObject) + " " + Tr("NotFound"), Severity.Error);
            _ = _logger.LogWarning(notFoundObject + " Not Found");
            return Task.CompletedTask;
        }

        public Task Required(string requiredObject)
        {
            _snack.Add(Tr(requiredObject) + " " + Tr("IsRequired"), Severity.Warning);
            // User validation error can be Warning
            _ = _logger.LogWarning(requiredObject + " Is Required (Validation)");
            return Task.CompletedTask;
        }

        public Task CompleteRequiredFields()
        {
            _snack.Add(Tr("PleaseCompleteTheRequiredFields"), Severity.Info);
            return Task.CompletedTask;
        }

        public Task AlreadyExists(string existedObject)
        {
            _snack.Add(Tr(existedObject) + " " + Tr("AlreadyExists"), Severity.Warning);
            _ = _logger.LogWarning(existedObject + " Already Exists");
            return Task.CompletedTask;
        }

        public Task Error(
            string errorReason,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (string.IsNullOrWhiteSpace(errorReason)) return Task.CompletedTask;
            if (ShouldSkip(errorReason)) return Task.CompletedTask;

            _snack.Add(Tr("ErrorIn") + ": " + Tr(errorReason), Severity.Error);
            _ = _logger.LogError(
                errorReason,
                action: "Error",
                callerMember: memberName,
                callerFile: filePath,
                callerLine: lineNumber);
            return Task.CompletedTask;
        }

        public async Task Error(
            List<string?> msgs,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var text = (msgs?.Where(m => !string.IsNullOrWhiteSpace(m)).ToArray() ?? Array.Empty<string?>())
                .Select(x => x!)
                .DefaultIfEmpty(Tr("UnexpectedErrorOccurred"))
                .ToArray();

            foreach (var m in text)
                await Error(m, filePath, memberName, lineNumber);
        }

        public Task Failed(string failReason)
        {
            _snack.Add(Tr("FailedTo") + ": " + Tr(failReason), Severity.Warning);
            _ = _logger.LogWarning(failReason, action: "Failed");
            return Task.CompletedTask;
        }

        public Task NotifyWarning(string reason)
        {
            _snack.Add(Tr(reason), Severity.Warning);
            _ = _logger.LogWarning(reason);
            return Task.CompletedTask;
        }

        public Task NotifyInfo(string info)
        {
            _snack.Add(Tr(info), Severity.Info);
            _ = _logger.LogInfo(info);
            return Task.CompletedTask;
        }

        public Task NotifySuccess(string info)
        {
            _snack.Add(Tr(info), Severity.Success);
            _ = _logger.LogInfo(info);
            return Task.CompletedTask;
        }

        public async Task<bool> ConfirmMsg(string confirmation, string yesTxt, string cancelTxt, string? headerTxt = "Warning")
        {
            var res = await _dialog.ShowMessageBox(
                Tr(headerTxt ?? "Warning") + "!",
                Tr(confirmation) + "?",
                yesText: Tr(yesTxt),
                cancelText: Tr(cancelTxt));

            return res == true;
        }

        public Task<bool> DltConfirmMsg()
            => ConfirmMsg("DoYouWantToDeleteThisItem", "Yes", "Cancel", "Warning");

        // ✅ رجّعنا دول زي ما طلبت
        public Task PublishErrors(
            string? Error,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!string.IsNullOrWhiteSpace(Error))
            {
                _snack.Add(Tr(Error), Severity.Error);
                _ = _logger.LogError(
                    Error,
                    action: "PublishErrors",
                    callerMember: memberName,
                    callerFile: filePath,
                    callerLine: lineNumber);
            }

            return Task.CompletedTask;
        }

        public Task PublishErrors(
            List<string> Errors,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (Errors is null || Errors.Count == 0) return Task.CompletedTask;

            foreach (var error in Errors)
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _snack.Add($"{Tr(error)}", Severity.Error);
                    _ = _logger.LogError(
                        error,
                        action: "PublishErrors",
                        callerMember: memberName,
                        callerFile: filePath,
                        callerLine: lineNumber);
                }

            return Task.CompletedTask;
        }

        public Task ClearAll()
        {
            _snack.Clear();
            return Task.CompletedTask;
        }

        /// <summary>
        /// ✅ DEV dialog محترم (في Debug فقط) + Copy icon
        /// </summary>
        public Task DevErrorDialogAsync(
        string title,
        string summary,
        string copyText,
        IReadOnlyList<string?>? chips = null,
        IReadOnlyDictionary<string, string>? meta = null,
        bool isValidation = false)
        {
#if DEBUG
            if (string.IsNullOrWhiteSpace(copyText)) return Task.CompletedTask;

            var dedupKey = "DEV|" + title + "|" + (copyText.Length > 500 ? copyText.Substring(0, 500) : copyText);
            if (ShouldSkip(dedupKey)) return Task.CompletedTask;

            var parameters = new DialogParameters
            {
                ["Title"] = title,
                ["Summary"] = summary ?? string.Empty,
                ["CopyText"] = copyText,
                ["Chips"] = (chips ?? Array.Empty<string?>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
                ["Meta"] = meta is null ? new Dictionary<string, string>() : new Dictionary<string, string>(meta),
                ["IsValidation"] = isValidation
            };

            var options = new DialogOptions
            {
                CloseButton = false,
                MaxWidth = MaxWidth.Large,
                FullWidth = true,
                NoHeader = true
            };

            _ = _dialog.ShowAsync<DevErrorDialog>(title, parameters, options);
#endif
            return Task.CompletedTask;
        }

        public async Task ExceptionHandle(
            string userMessage,
            Exception ex,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0,
            Func<Exception, Task>? logError = null)
        {
            await Error(userMessage);

            // Log explicit exception via logger (handled in Error call? No, error just logs string)
            // But usually this calls Error(msg) which logs User Error.
            // We should also log the Exception details to ClientLogger if not done already.
            _ = _logger.LogError(
                userMessage,
                action: "ExceptionHandle",
                ex: ex,
                callerMember: memberName,
                callerFile: filePath,
                callerLine: lineNumber);

#if DEBUG
            var file = System.IO.Path.GetFileName(filePath);
            var details =
$@"────────── DEV EXCEPTION ──────────
Message   : {ex.Message}
Caller    : {file}::{memberName} (line {lineNumber})
FilePath  : {filePath}
Time      : {DateTimeOffset.Now:O}

StackTrace:
{ex}
──────────────────────────────────";

            await DevErrorDialogAsync(
            title: "DEV: Exception",
            summary: userMessage,
            copyText: details,
            chips: new[] { ex.Message },
            meta: new Dictionary<string, string>
            {
                ["Caller"] = $"{file}::{memberName} (line {lineNumber})",
                ["Time"] = $"{DateTimeOffset.Now:O}"
            },
            isValidation: false);

#endif

            if (logError is not null)
                await logError(ex).ConfigureAwait(false);
        }

        static bool ShouldSkip(string msg)
        {
            var now = DateTime.UtcNow;
            if (_recent.TryGetValue(msg, out var ts) && (now - ts) < DedupWindow)
                return true;

            _recent[msg] = now;

            foreach (var k in _recent.Keys.ToList())
                if ((now - _recent[k]) > TimeSpan.FromMinutes(1))
                    _recent.Remove(k);

            return false;
        }
    }
}
