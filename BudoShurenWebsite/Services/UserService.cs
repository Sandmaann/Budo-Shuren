using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace BudoShurenWebsite.Services
{
    public class UserService
    {
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        public UserService(AuthenticationStateProvider authenticationStateProvider)
        {
            _authenticationStateProvider = authenticationStateProvider;
        }

        ClaimsPrincipal _current = null;

        public ClaimsPrincipal Current
        {
            get
            {
                if (_current == null) throw new Exception("Not initialized"); return _current;
            }
        }

        public async Task InitializeAsync()
        {
            if (_current != null)
                return;
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            _current = authState.User;
        }
    }
}