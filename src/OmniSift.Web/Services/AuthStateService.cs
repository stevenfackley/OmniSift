using Microsoft.JSInterop;

namespace OmniSift.Web.Services;

/// <summary>
/// Persists the JWT token + tenant/user info to localStorage and provides reactive state.
/// Notifies subscribers when auth changes so NavMenu can update live.
/// </summary>
public sealed class AuthStateService(IJSRuntime js)
{
    private const string TokenKey = "omnisift_token";
    private const string TenantKey = "omnisift_tenant";

    private string? _token;
    private Guid _tenantId;

    public event Action? OnAuthChanged;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public string? Token => _token;
    public Guid TenantId => _tenantId;

    /// <summary>
    /// Called once on startup to hydrate from localStorage.
    /// </summary>
    public async Task InitializeAsync()
    {
        _token = await js.InvokeAsync<string?>("localStorage.getItem", TokenKey).ConfigureAwait(false);
        var tenantStr = await js.InvokeAsync<string?>("localStorage.getItem", TenantKey).ConfigureAwait(false);
        if (Guid.TryParse(tenantStr, out var tid))
            _tenantId = tid;
    }

    public async Task SignInAsync(string token, Guid tenantId)
    {
        _token = token;
        _tenantId = tenantId;
        await js.InvokeVoidAsync("localStorage.setItem", TokenKey, token).ConfigureAwait(false);
        await js.InvokeVoidAsync("localStorage.setItem", TenantKey, tenantId.ToString()).ConfigureAwait(false);
        OnAuthChanged?.Invoke();
    }

    public async Task SignOutAsync()
    {
        _token = null;
        _tenantId = Guid.Empty;
        await js.InvokeVoidAsync("localStorage.removeItem", TokenKey).ConfigureAwait(false);
        await js.InvokeVoidAsync("localStorage.removeItem", TenantKey).ConfigureAwait(false);
        OnAuthChanged?.Invoke();
    }
}
