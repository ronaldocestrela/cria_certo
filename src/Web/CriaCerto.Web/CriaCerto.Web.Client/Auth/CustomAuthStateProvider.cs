using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;
using System.Text.Json;

namespace CriaCerto.Web.Client.Auth;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());
    private AuthenticationState? _cachedState;

    public CustomAuthStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return _cachedState ?? new AuthenticationState(_anonymous);
        }

        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            if (string.IsNullOrWhiteSpace(token))
            {
                _cachedState = new AuthenticationState(_anonymous);
                return _cachedState;
            }

            if (!TryCreateAuthenticatedState(token, out var authenticatedState))
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
                _cachedState = new AuthenticationState(_anonymous);
                return _cachedState;
            }

            _cachedState = authenticatedState;
            return authenticatedState;
        }
        catch (JSException)
        {
            return _cachedState ?? new AuthenticationState(_anonymous);
        }
        catch (InvalidOperationException)
        {
            return _cachedState ?? new AuthenticationState(_anonymous);
        }
    }

    public async Task MarkUserAsAuthenticated(string token)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
        var authState = TryCreateAuthenticatedState(token, out var authenticatedState)
            ? authenticatedState
            : new AuthenticationState(_anonymous);
        _cachedState = authState;
        NotifyAuthenticationStateChanged(Task.FromResult(authState));
    }

    public async Task MarkUserAsLoggedOut()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
        _cachedState = new AuthenticationState(_anonymous);
        NotifyAuthenticationStateChanged(Task.FromResult(_cachedState));
    }

    private bool TryCreateAuthenticatedState(string token, out AuthenticationState authState)
    {
        authState = new AuthenticationState(_anonymous);

        try
        {
            var claims = ParseClaimsFromJwt(token).ToList();
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            if (expClaim != null && long.TryParse(expClaim, out var expSeconds))
            {
                var expirationDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                if (expirationDate <= DateTimeOffset.UtcNow)
                {
                    return false;
                }
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);
            authState = new AuthenticationState(user);
            return user.Identity?.IsAuthenticated == true;
        }
        catch
        {
            return false;
        }
    }

    internal static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs != null)
        {
            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        claims.Add(new Claim(kvp.Key, item.ToString()));
                    }
                }
                else
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.ToString() ?? ""));
                }
            }
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
