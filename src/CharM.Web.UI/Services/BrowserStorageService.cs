using Microsoft.JSInterop;

namespace CharM.Web.Services;

/// <summary>
/// Thin wrapper around <c>wwwroot/js/browser-storage.js</c> for persisting
/// the active character to <c>window.localStorage</c>. Single-slot today
/// (key <c>charm:character:active</c>) — multi-character catalog is a
/// future extension.
///
/// <para>All methods are safe to call only AFTER the first interactive
/// render. During Blazor's static prerender pass JSInterop is unavailable;
/// callers must guard with <c>OnAfterRenderAsync(firstRender: true)</c> or
/// equivalent. The methods themselves swallow JS-side failures (quota
/// exceeded, private-mode block, missing module) and return null/false so
/// the in-memory session keeps working.</para>
/// </summary>
public sealed class BrowserStorageService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public BrowserStorageService(IJSRuntime js)
    {
        _js = js;
    }

    private async Task<IJSObjectReference?> GetModuleAsync()
    {
        if (_module is not null) return _module;
        try
        {
            // browser-storage.js lives in this Razor Class Library's wwwroot,
            // which the static-web-assets pipeline serves under
            // /_content/CharM.Web.UI/. The relative "./js/browser-storage.js"
            // path resolved against the page URL (e.g. /, /powers) and 404'd,
            // so the catch below silently turned every persistence call into
            // a no-op — characters never survived a reload.
            _module = await _js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/CharM.Web.UI/js/browser-storage.js");
            return _module;
        }
        catch
        {
            // Static prerender, missing file, or interop temporarily down.
            return null;
        }
    }

    /// <summary>Read the persisted active character as base64 .dnd4e XML, or null when none.</summary>
    public async Task<string?> GetActiveCharacterAsync()
    {
        try
        {
            return await _js.InvokeAsync<string?>("charm.browserStorage.getActiveCharacter");
        }
        catch
        {
            // Fall through to the Razor Class Library module for hosts that
            // don't inject the global browserStorage bridge (for example MAUI).
        }

        var module = await GetModuleAsync();
        if (module is null) return null;
        try
        {
            return await module.InvokeAsync<string?>("getActiveCharacter");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Save base64 .dnd4e XML as the active character. Returns false on storage failure.</summary>
    public async Task<bool> SetActiveCharacterAsync(string base64Xml)
    {
        try
        {
            return await _js.InvokeAsync<bool>("charm.browserStorage.setActiveCharacter", base64Xml);
        }
        catch
        {
            // Fall through to the RCL module for non-web hosts.
        }

        var module = await GetModuleAsync();
        if (module is null) return false;
        try
        {
            return await module.InvokeAsync<bool>("setActiveCharacter", base64Xml);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Clear the active-character slot. Idempotent.</summary>
    public async Task ClearActiveCharacterAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("charm.browserStorage.clearActiveCharacter");
            return;
        }
        catch
        {
            // Fall through to the RCL module for non-web hosts.
        }

        var module = await GetModuleAsync();
        if (module is null) return;
        try
        {
            await module.InvokeVoidAsync("clearActiveCharacter");
        }
        catch
        {
            // ignore
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); }
            catch { /* circuit may already be torn down */ }
        }
    }
}
