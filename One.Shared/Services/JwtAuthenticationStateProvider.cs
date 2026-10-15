using Microsoft.AspNetCore.Components.Authorization;
using One.Shared.Services.Session;

namespace One.Shared.Services
{
    public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
    {
        private readonly IClientSession _session;

        public JwtAuthenticationStateProvider(IClientSession session)
        {
            _session = session;
            _session.Changed += OnSessionChanged;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            await _session.InitializeAsync();
            return new AuthenticationState(_session.User);
        }

        private void OnSessionChanged() =>
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_session.User)));

        public void Dispose() => _session.Changed -= OnSessionChanged;
    }
}
