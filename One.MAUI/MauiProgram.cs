using AKSoftware.Localization.MultiLanguages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using One.MAUI.Services.Platform;
using One.MAUI.Services.SignalRHub;
using One.Shared.Configuration;
using One.Shared.Services.ErrorNotifier; // IGlobalErrorNotifier
using One.Shared.Services.Logging;
using One.Shared.Services.Platform;
using System.Net;

namespace One.MAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            AddPackagedAppSettings(builder);
            AddLocalOverrideIfExists(builder);

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
            builder.Services.AddScoped<IClientStorage, MauiClientStorage>();
            builder.Services.AddOneShared();
            builder.Services.AddOneApiClient(GetApiBaseAddress(builder.Configuration), CreatePrimaryHandler);
            builder.Services.AddScoped<MainHub>();

            // ✅ Logging Service
            builder.Services.AddScoped<IClientLogger, One.MAUI.Services.Logging.ClientLogger>();

            // ✅ Global notifier (لو مش متسجل)
            builder.Services.AddSingleton<IGlobalErrorNotifier, GlobalErrorNotifier>();

            var app = builder.Build();

            return app;
        }

        private static Uri GetApiBaseAddress(IConfiguration configuration)
        {
            var value = configuration["ApiBaseUrl"];
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("ApiBaseUrl is missing or invalid in the platform configuration.");
            }

            return uri;
        }

        private static HttpMessageHandler CreatePrimaryHandler()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                MaxConnectionsPerServer = 20
            };

#if DEBUG
            // WARNING: This bypasses SSL certificate validation and should NEVER be enabled in production!
            // This is only acceptable for local development with self-signed certificates.
            System.Diagnostics.Debug.WriteLine("[Security Warning] SSL certificate validation is disabled. This is only safe for local development.");
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif

            return handler;
        }

        private static void AddPackagedAppSettings(MauiAppBuilder builder)
        {
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json")
                                             .GetAwaiter().GetResult();
                builder.Configuration.AddJsonStream(stream);

                var platformFile = DeviceInfo.Platform == DevicePlatform.Android
                    ? "appsettings.android.json"
                    : DeviceInfo.Platform == DevicePlatform.WinUI
                        ? "appsettings.windows.json"
                        : DeviceInfo.Platform == DevicePlatform.iOS
                            ? "appsettings.ios.json"
                            : DeviceInfo.Platform == DevicePlatform.macOS
                                ? "appsettings.maccatalyst.json"
                                : null;
                if (platformFile is not null)
                {
                    using var platformStream = FileSystem.OpenAppPackageFileAsync(platformFile)
                        .GetAwaiter().GetResult();
                    builder.Configuration.AddJsonStream(platformStream);
                }
            }
            catch (Exception ex)
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>());
                System.Diagnostics.Debug.WriteLine($"[Config] appsettings.json not found in package: {ex.Message}");
            }
        }

        private static void AddLocalOverrideIfExists(MauiAppBuilder builder)
        {
            var local = Path.Combine(FileSystem.AppDataDirectory, "appsettings.local.json");
            if (File.Exists(local))
                builder.Configuration.AddJsonFile(local, optional: true, reloadOnChange: false);
        }

    }
}
