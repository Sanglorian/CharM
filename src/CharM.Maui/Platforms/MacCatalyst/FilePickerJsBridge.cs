using System;
using Foundation;
using WebKit;

namespace CharM.Maui;

/// <summary>
/// Installs a browser-side probe that reports <c>click</c> and
/// <c>change</c> events on file inputs (and their associated labels)
/// back to native code via a <see cref="IWKScriptMessageHandler"/>.
/// This is the half of the file-picker debug instrumentation that
/// tells us whether the click actually reaches the WebView in the
/// first place. If the native <c>RunOpenPanel</c> never fires but the
/// browser <c>[js] click</c> log lines DO appear, the regression is in
/// WebKit's open-panel dispatch (or our <c>UIDelegate</c> wiring),
/// not in the front-end label/input plumbing.
/// </summary>
internal static class FilePickerJsBridge
{
    private const string HandlerName = "charmFilePickerProbe";

    // Delegated capture-phase listeners on `document` so the probe
    // survives Blazor route changes that swap big DOM subtrees. The
    // outer IIFE is idempotent — running it twice just rebinds the
    // listeners, which is harmless because we removeEventListener
    // first.
    private const string ProbeScript = """
        (function () {
            if (!window.webkit || !window.webkit.messageHandlers
                || !window.webkit.messageHandlers.charmFilePickerProbe) {
                return;
            }
            var bridge = window.webkit.messageHandlers.charmFilePickerProbe;
            function post(payload) {
                try { bridge.postMessage(payload); } catch (e) { /* no-op */ }
            }
            function describe(el) {
                if (!el) return null;
                return {
                    tag: el.tagName || null,
                    type: el.type || null,
                    id: el.id || null,
                    name: el.name || null,
                    htmlFor: el.htmlFor || null,
                    cls: typeof el.className === 'string'
                        ? el.className.slice(0, 120) : null
                };
            }
            function isFileInput(el) {
                return el && el.tagName === 'INPUT'
                    && (el.type || '').toLowerCase() === 'file';
            }
            function isLabel(el) {
                return el && el.tagName === 'LABEL';
            }
            function onClick(e) {
                var t = e.target;
                if (isFileInput(t) || isLabel(t)) {
                    post({ event: 'click', target: describe(t),
                        readyState: document.readyState });
                }
            }
            function onChange(e) {
                if (isFileInput(e.target)) {
                    post({ event: 'change', target: describe(e.target),
                        files: e.target.files ? e.target.files.length : 0 });
                }
            }
            document.removeEventListener('click', onClick, true);
            document.removeEventListener('change', onChange, true);
            document.addEventListener('click', onClick, true);
            document.addEventListener('change', onChange, true);
            post({ event: 'probe-installed',
                readyState: document.readyState,
                url: location.href });
        })();
        """;

    public static void Install(WKWebView webView)
    {
        try
        {
            var ucc = webView.Configuration.UserContentController;

            // Remove any prior handler with the same name so re-init
            // (e.g. on iOS where the WebView is recreated) doesn't
            // throw NSInvalidArgumentException.
            try { ucc.RemoveScriptMessageHandler(HandlerName); }
            catch { /* not registered yet on first run — fine */ }

            ucc.AddScriptMessageHandler(new ProbeHandler(), HandlerName);

            webView.EvaluateJavaScript(new NSString(ProbeScript), (result, error) =>
            {
                if (error is not null)
                    FilePickerLog.Warn("js-probe", $"EvaluateJavaScript failed: {error.LocalizedDescription}");
                else
                    FilePickerLog.Info("js-probe", "EvaluateJavaScript completed");
            });

            FilePickerLog.Info("js-probe", $"Bridge installed (handler='{HandlerName}')");
        }
        catch (Exception ex)
        {
            FilePickerLog.Error("js-probe", "Install failed", ex);
        }
    }

    private sealed class ProbeHandler : NSObject, IWKScriptMessageHandler
    {
        public void DidReceiveScriptMessage(
            WKUserContentController userContentController,
            WKScriptMessage message)
        {
            try
            {
                // Body comes back as NSDictionary for plain JS objects.
                // Stringify via JSON for a single line that's grep-friendly.
                string serialized;
                if (message.Body is NSDictionary dict)
                {
                    var data = NSJsonSerialization.Serialize(dict, 0, out var err);
                    serialized = (err is null && data is not null)
                        ? NSString.FromData(data, NSStringEncoding.UTF8)?.ToString() ?? dict.ToString()
                        : dict.ToString();
                }
                else
                {
                    serialized = message.Body?.ToString() ?? "<null>";
                }

                FilePickerLog.Info("js", serialized);
            }
            catch (Exception ex)
            {
                FilePickerLog.Error("js", "DidReceiveScriptMessage handler threw", ex);
            }
        }
    }
}
