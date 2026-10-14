namespace One.MAUI
{
    public partial class App : Application
    {
        public App(One.Shared.Services.ErrorNotifier.IGlobalErrorNotifier notifier)
        {
            InitializeComponent();

            AppDomain.CurrentDomain.UnhandledException += (_, e) => _ = LogAndShowAsync(notifier, e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) => { _ = LogAndShowAsync(notifier, e.Exception); e.SetObserved(); };

#if ANDROID
            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            {
                _ = LogAndShowAsync(notifier, e.Exception);
                e.Handled = true; // امنع الكراش قدر الإمكان
            };
#endif
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "One.ERP" };
        }

        private static async Task LogAndShowAsync(
            One.Shared.Services.ErrorNotifier.IGlobalErrorNotifier notifier,
            Exception? ex)
        {
            if (ex is not null)
                await notifier.NotifyExceptionAsync("Unexpected application error", ex);

            try
            {
                var path = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
                File.AppendAllText(path, $"[{DateTime.Now}] {ex}\n\n");
            }
            catch { /* تجاهل */ }

            MainThread.BeginInvokeOnMainThread(static async () =>
            {
                try
                {
                    // Use the first window's page for single-window applications to avoid obsolete MainPage usage
                    var windows = Application.Current?.Windows;
                    var page = (windows != null && windows.Count > 0) ? windows[0].Page : null;
                    if (page != null)
                    {
                        await page.DisplayAlertAsync("Oops", "Unexpected error occurred.", "OK");
                    }
                }
                catch { /* تجاهل */ }
            });
        }
    }
}
