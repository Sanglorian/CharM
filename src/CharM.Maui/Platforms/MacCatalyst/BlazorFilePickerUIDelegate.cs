using System;
using System.Linq;
using Foundation;
using UIKit;
using WebKit;

namespace CharM.Maui;

/// <summary>
/// WKWebView UI delegate that handles the <c>&lt;input type="file"&gt;</c>
/// open-panel request. On Mac Catalyst (and iOS) <c>BlazorWebView</c>'s
/// underlying <c>WKWebView</c> does not ship a delegate for
/// <c>webView:runOpenPanelWithParameters:initiatedByFrame:completionHandler:</c>
/// so the native file picker never appears — InputFile clicks silently no-op
/// and only drag-and-drop works. Wiring this delegate up restores the
/// expected behavior by presenting a <see cref="UIDocumentPickerViewController"/>
/// and returning its picked URLs to WebKit via the supplied completion
/// handler. Apple's contract: invoke <c>completionHandler</c> exactly once
/// with the picked URLs, or with <c>null</c> on cancel.
/// </summary>
internal sealed class BlazorFilePickerUIDelegate : WKUIDelegate
{
    public override void RunOpenPanel(
        WKWebView webView,
        WKOpenPanelParameters parameters,
        WKFrameInfo frame,
        Action<NSUrl[]> completionHandler)
    {
        FilePickerLog.Info("RunOpenPanel",
            $"called (allowsMultipleSelection={parameters.AllowsMultipleSelection}, " +
            $"frame.MainFrame={frame.MainFrame})");

        // WKOpenPanelParameters doesn't expose the original <input accept="…">
        // values, so accept any pickable item and let the calling Blazor
        // code filter (it does its own validation on file content anyway).
        var allowedUtis = new[] { "public.item", "public.data", "public.content" };

#pragma warning disable CA1416 // Constructor is marked iOS 14+ obsolete but still works
#pragma warning disable CA1422 // Validate platform compatibility
        // UIDocumentPickerMode.Import copies the picked file into the app
        // sandbox so the URL stays readable after the picker dismisses,
        // matching <input type="file"> upload semantics.
        var picker = new UIDocumentPickerViewController(allowedUtis, UIDocumentPickerMode.Import);
#pragma warning restore CA1422
#pragma warning restore CA1416

        picker.AllowsMultipleSelection = parameters.AllowsMultipleSelection;
        picker.ShouldShowFileExtensions = true;

        // Guard against double-fire (DidPickDocument + WasCancelled both
        // raising); WebKit will deadlock or assert if the completion handler
        // runs more than once.
        var done = false;
        EventHandler<UIDocumentPickedAtUrlsEventArgs>? pickHandler = null;
        EventHandler? cancelHandler = null;

        void Finish(NSUrl[]? urls)
        {
            if (done) return;
            done = true;
            if (pickHandler is not null) picker.DidPickDocumentAtUrls -= pickHandler;
            if (cancelHandler is not null) picker.WasCancelled -= cancelHandler;
            FilePickerLog.Info("RunOpenPanel",
                urls is null
                    ? "Finish(null) — cancel or no presenter"
                    : $"Finish(urls.Length={urls.Length})");
            completionHandler(urls!);
        }

        pickHandler = (_, args) =>
        {
            FilePickerLog.Info("RunOpenPanel", $"DidPickDocumentAtUrls fired (urls={args.Urls?.Length ?? 0})");
            Finish(args.Urls);
        };
        cancelHandler = (_, _) =>
        {
            FilePickerLog.Info("RunOpenPanel", "WasCancelled fired");
            Finish(null);
        };

        picker.DidPickDocumentAtUrls += pickHandler;
        picker.WasCancelled += cancelHandler;

        var presenter = FindPresentingViewController(webView)
            ?? GetKeyWindow()?.RootViewController;

        FilePickerLog.Info("RunOpenPanel",
            $"presenter resolved to: {presenter?.GetType().FullName ?? "<null>"}");

        if (presenter is null)
        {
            // No view controller to host the picker — bail out gracefully
            // rather than leaving WebKit waiting forever on the handler.
            FilePickerLog.Warn("RunOpenPanel", "no presenter available, bailing out");
            Finish(null);
            return;
        }

        try
        {
            presenter.PresentViewController(picker, animated: true, completionHandler: () =>
                FilePickerLog.Info("RunOpenPanel", "PresentViewController completion fired (picker visible)"));
            FilePickerLog.Info("RunOpenPanel", "PresentViewController returned (presentation begun)");
        }
        catch (Exception ex)
        {
            FilePickerLog.Error("RunOpenPanel", "PresentViewController threw", ex);
            Finish(null);
        }
    }

    private static UIViewController? FindPresentingViewController(UIView view)
    {
        var responder = view.NextResponder;
        while (responder is not null)
        {
            if (responder is UIViewController vc) return vc;
            responder = responder.NextResponder;
        }
        return null;
    }

    private static UIWindow? GetKeyWindow()
    {
        foreach (var scene in UIApplication.SharedApplication.ConnectedScenes.ToArray())
        {
            if (scene is UIWindowScene ws)
            {
                foreach (var window in ws.Windows)
                {
                    if (window.IsKeyWindow) return window;
                }
            }
        }
        return null;
    }
}
