using Android.App;
using Android.Runtime;

namespace CharM.Maui;

[Application(Debuggable = true)]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        // Enable Chrome/Edge DevTools attach to the BlazorWebView via
        // chrome://inspect/#devices. Set here (in MainApplication.OnCreate)
        // in addition to MainActivity.OnCreate as belt-and-suspenders —
        // MainApplication runs before any Activity, so any WebView created
        // during MAUI's own bootstrap sees the flag.
        // Pre-release; revisit before shipping a public build.
        Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true);

        base.OnCreate();
    }

    protected override MauiApp CreateMauiApp()
        => MauiProgram.CreateMauiApp();
}
