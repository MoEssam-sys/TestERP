using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using One.Shared.Configuration;
using One.Shared.Services;
using One.Shared.Services.Logging;
using One.Shared.Services.Platform;
using One.Web;
using One.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<IClientStorage, BrowserClientStorage>();
builder.Services.AddScoped<IClientLogger, BrowserClientLogger>();
builder.Services.AddOneShared();
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is missing from WebAssembly configuration.");
builder.Services.AddOneApiClient(new Uri(apiBaseUrl, UriKind.Absolute));

await builder.Build().RunAsync();
