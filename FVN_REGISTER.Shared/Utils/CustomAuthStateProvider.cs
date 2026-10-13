using FVN_REGISTER.Shared.Utils.Helpers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Claims;

namespace FVN_REGISTER.Shared.Utils
{
    public sealed class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ILogger<CustomAuthStateProvider> _logger;
        private readonly bool _debug;
        private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

        public string? CurrentToken { get; set; }

        public CustomAuthStateProvider(
            ILogger<CustomAuthStateProvider> logger,
            IConfiguration config)
        {
            _logger = logger;
            _debug = config.GetValue<bool>("AuthDebug:Enabled");
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentToken))
                    return Task.FromResult(new AuthenticationState(_anonymous));

                var claims = JwtParser.ParseClaimsFromJwt(CurrentToken);
                if (IsExpired(claims))
                {
                    CurrentToken = null;
                    return Task.FromResult(new AuthenticationState(_anonymous));
                }

                var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
                return Task.FromResult(new AuthenticationState(user));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[AuthState] Invalid token");
                return Task.FromResult(new AuthenticationState(_anonymous));
            }
        }

        /// <summary>
        /// Restores the bearer token after the interactive Blazor circuit is ready.
        /// Call from OnAfterRenderAsync, never during prerender/initialization.
        /// </summary>
        public async Task RestoreFromStorageAsync(ITokenStorage tokenStorage)
        {
            if (!string.IsNullOrWhiteSpace(CurrentToken))
                return;

            try
            {
                var token = (await tokenStorage.GetTokenAsync())?.Trim('"').Trim();
                if (string.IsNullOrWhiteSpace(token))
                    return;

                var claims = JwtParser.ParseClaimsFromJwt(token);
                if (IsExpired(claims))
                {
                    await tokenStorage.RemoveTokenAsync();
                    await tokenStorage.RemoveRefreshTokenAsync();
                    if (_debug)
                        _logger.LogInformation("[AuthState] Expired stored token cleared.");
                    return;
                }

                CurrentToken = token;
                var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
                if (_debug)
                    _logger.LogInformation("[AuthState] Stored token restored.");

                NotifyAuthenticationStateChanged(
                    Task.FromResult(new AuthenticationState(user)));
            }
            catch (Exception ex)
            {
                CurrentToken = null;
                _logger.LogWarning(ex, "[AuthState] Could not restore stored token.");
            }
        }

        private static bool IsExpired(IEnumerable<Claim> claims)
        {
            var exp = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            return long.TryParse(exp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                && DateTimeOffset.FromUnixTimeSeconds(seconds) <= DateTimeOffset.UtcNow;
        }

        public void NotifyUserLogin(string token)
        {
            try
            {
                var normalizedToken = token.Trim('"').Trim();
                var claims = JwtParser.ParseClaimsFromJwt(normalizedToken);
                if (IsExpired(claims))
                    throw new InvalidOperationException("Cannot establish an authentication state from an expired token.");

                CurrentToken = normalizedToken;
                var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

                if (_debug)
                    _logger.LogInformation("[AuthState] User login notified");

                NotifyAuthenticationStateChanged(
                    Task.FromResult(new AuthenticationState(user)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthState] NotifyUserLogin failed");
                NotifyUserLogout();
            }
        }

        public void NotifyUserLogout()
        {
            CurrentToken = null;

            if (_debug)
                _logger.LogInformation("[AuthState] User logout");

            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(_anonymous)));
        }
    }
}