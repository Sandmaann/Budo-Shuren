using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace BudoShurenWebsite.Services
{
    public class UserService
    {
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly UserManager<ApplicationUser> _userManager;
        public UserService(UserManager<ApplicationUser> userManager, AuthenticationStateProvider authenticationStateProvider)
        {
            _userManager = userManager;
            _authenticationStateProvider = authenticationStateProvider;
        }

        ClaimsPrincipal _current = null;
        public ClaimsPrincipal Current
        {
            get
            {
                if (_current == null)
                    throw new Exception("Not initialized");
                return _current;
            }
        }

        UserWithRoles _currentUser = null;
        public UserWithRoles CurrentUser
        {
            get
            {
                if (_currentUser == null)
                    throw new Exception("Not initialized");
                return _currentUser;
            }
        }

        public async Task InitializeAsync()
        {
            if (_current != null)
                return;
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            _current = authState.User;

            var currentUser = await _userManager.GetUserAsync(_current);
            var roles = await _userManager.GetRolesAsync(currentUser);

            _currentUser = new UserWithRoles { User = currentUser, Roles = roles };
        }
    }
}