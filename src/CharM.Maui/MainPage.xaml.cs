using Microsoft.AspNetCore.Components.WebView;

namespace CharM.Maui;

public partial class MainPage : ContentPage
{
#if MACCATALYST
    // Hold a strong reference: WKWebView's UIDelegate is declared weak in
    // Apple's headers, so without this the picker delegate could be
    // garbage collected and the file picker would silently stop working.
    private BlazorFilePickerUIDelegate? _filePickerDelegate;
#endif

    public MainPage()
    {
        InitializeComponent();
    }

    private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
#if MACCATALYST
        FilePickerLog.Info("init",
            $"OnBlazorWebViewInitialized fired (e.WebView type: {e.WebView?.GetType().FullName ?? "<null>"})");

        // WKWebView in BlazorWebView ships without a UIDelegate, which means
        // <input type="file"> click events are dropped on Mac Catalyst (and
        // iOS). Drag-and-drop works because that's a separate WebKit code
        // path, but the file picker UI never appears. Installing a delegate
        // that maps RunOpenPanel → UIDocumentPickerViewController restores
        // the expected behavior. See BlazorFilePickerUIDelegate.cs.
        if (e.WebView is WebKit.WKWebView wkWebView)
        {
            _filePickerDelegate = new BlazorFilePickerUIDelegate();
            wkWebView.UIDelegate = _filePickerDelegate;

            var stuck = wkWebView.UIDelegate;
            FilePickerLog.Info("init",
                $"UIDelegate assigned. Immediate read-back: {stuck?.GetType().FullName ?? "<null>"} " +
                $"(matches: {ReferenceEquals(stuck, _filePickerDelegate)})");

            FilePickerJsBridge.Install(wkWebView);

            // The UIDelegate property is weak; even though we hold a strong
            // ref via _filePickerDelegate, something on the MAUI / WebKit
            // side could replace it later (re-init, layout pass, content
            // controller swap). Poll a few times after init so a late
            // replacement shows up clearly in the log instead of silently
            // breaking the picker.
            SchedulePostInitDelegateChecks(wkWebView);
        }
        else
        {
            FilePickerLog.Warn("init",
                $"e.WebView is NOT a WKWebView — file-picker delegate NOT installed. " +
                $"Actual type: {e.WebView?.GetType().FullName ?? "<null>"}");
        }
#endif
    }

#if MACCATALYST
    private void SchedulePostInitDelegateChecks(WebKit.WKWebView wkWebView)
    {
        int[] delaysMs = { 1000, 5000, 15000, 30000 };
        foreach (var ms in delaysMs)
        {
            var capturedMs = ms;
            _ = Task.Delay(capturedMs).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        var current = wkWebView.UIDelegate;
                        var matches = ReferenceEquals(current, _filePickerDelegate);
                        var line =
                            $"t+{capturedMs}ms UIDelegate={current?.GetType().FullName ?? "<null>"} " +
                            $"(matches expected: {matches})";
                        if (matches)
                            FilePickerLog.Info("recheck", line);
                        else
                            FilePickerLog.Warn("recheck", $"UIDELEGATE REPLACED! {line}");
                    }
                    catch (Exception ex)
                    {
                        FilePickerLog.Error("recheck", $"t+{capturedMs}ms check threw", ex);
                    }
                });
            }, TaskScheduler.Default);
        }
    }
#endif
}
