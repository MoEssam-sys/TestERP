using System.Net.Http.Headers;
using One.Shared.Services.Session;
using Microsoft.AspNetCore.Components;

namespace One.Shared.Services;

public sealed class ApiAuthorizationHandler(
    IClientSession session,
    NavigationManager navigation) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.AccessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        var page = "/" + navigation.ToBaseRelativePath(navigation.Uri);
        if (page.Length <= 500 && !page.StartsWith("//", StringComparison.Ordinal))
            request.Headers.TryAddWithoutValidation("X-One-Page", page);

        return base.SendAsync(request, cancellationToken);
    }
}
