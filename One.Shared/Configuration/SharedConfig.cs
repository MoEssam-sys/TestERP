using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using AKSoftware.Localization.MultiLanguages;
using AKSoftware.Localization.MultiLanguages.Providers;
using AKSoftware.Localization.MultiLanguages.SourceGenerator;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using One.Domain.Localization;
using One.Shared.Localization;
using One.Shared.Services;
using One.Shared.Services.Api;
using One.Shared.Services.ErrorNotifier;
using One.Shared.Services.Session;
using One.Shared.Themes.MudBlazorThemes;

namespace One.Shared.Configuration
{
    public static class SharedConfig
    {
        public static IServiceCollection AddOneShared(this IServiceCollection services)
        {
            services.AddMudServices(config =>
            {
                config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;     //~ Set the position of snackbars to top-right
                config.SnackbarConfiguration.PreventDuplicates = true;   //~ Prevent duplicate snackbars
                config.SnackbarConfiguration.NewestOnTop = true;     //~ Display the newest snackbar on top
                config.SnackbarConfiguration.ShowCloseIcon = true;   //~ Show a close icon on the snackbars
                config.SnackbarConfiguration.VisibleStateDuration = 5000;    //~ Set the duration a snackbar is visible to 5000 milliseconds
                config.SnackbarConfiguration.HideTransitionDuration = 200;   //~ Set the duration of the hide transition to 200 milliseconds
                config.SnackbarConfiguration.ShowTransitionDuration = 300;   //~ Set the duration of the show transition to 300 milliseconds
                config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;   //~ Set the snackbar variant to filled
                config.SnackbarConfiguration.ClearAfterNavigation = false;   //~ Do not clear snackbars after navigation
                config.SnackbarConfiguration.MaxDisplayedSnackbars = 3;  //~ Set the maximum number of displayed snackbars to 3
            });

            services.AddAuthorizationCore();
            services.AddLanguageContainer<EmbeddedResourceKeysProvider>(
                typeof(SharedConfig).Assembly,
                "Resources");
            services.AddKeysAccessorAsScoped();
            services.AddSingleton<ITranslator, LanguageContainerTranslator>();

            services.AddScoped<UiState>();
            services.AddScoped<EffectivePermissionState>();

            services.AddScoped<StateService>();
            services.AddScoped<MessagesCenter>();
            services.AddScoped<IClientSession, ClientSession>();
            services.AddScoped<JwtAuthenticationStateProvider>();
            services.AddScoped<AuthenticationStateProvider>(sp =>
                sp.GetRequiredService<JwtAuthenticationStateProvider>());
            services.AddScoped<JsInteropService>();

            return services;
        }

        public static IServiceCollection AddOneApiClient(
            this IServiceCollection services,
            Uri baseAddress,
            Func<HttpMessageHandler>? primaryHandlerFactory = null)
        {
            if (!baseAddress.IsAbsoluteUri ||
                (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("ApiBaseUrl must be an absolute HTTP or HTTPS address.");
            }

            services.AddScoped<IOneApiClient>(serviceProvider =>
            {
                var authorizationHandler = new ApiAuthorizationHandler(
                    serviceProvider.GetRequiredService<IClientSession>(),
                    serviceProvider.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>())
                {
                    InnerHandler = primaryHandlerFactory?.Invoke() ?? new HttpClientHandler()
                };
                var httpClient = new HttpClient(authorizationHandler)
                {
                    BaseAddress = baseAddress,
                    Timeout = TimeSpan.FromSeconds(30)
                };
                return new OneApiClient(
                    httpClient,
                    serviceProvider.GetRequiredService<IClientSession>(),
                    serviceProvider.GetRequiredService<MessagesCenter>());
            });

            return services;
        }
    }
}
