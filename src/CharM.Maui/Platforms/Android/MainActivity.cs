using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace CharM.Maui;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges =
        ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Enable Chrome DevTools attach to the BlazorWebView via
        // chrome://inspect/#devices. Required to see Console/Network for
        // diagnosing white-screen "Loading..." issues. Pre-release; revisit
        // before shipping to a real Play Store / public side-load build.
        Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true);

        base.OnCreate(savedInstanceState);

        // Android 15+ (API 35) forces edge-to-edge: the window draws under the
        // status and navigation bars, so our top/bottom navs end up beneath OS
        // chrome. Re-inset the content view by the system-bar + display-cutout
        // insets so the Blazor UI lives entirely inside the safe area. Using
        // AndroidX ViewCompat keeps this working on older API levels too.
        var content = FindViewById(Android.Resource.Id.Content);
        if (content is not null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(content, new SafeAreaInsetsListener());
            ViewCompat.RequestApplyInsets(content);
        }
    }

    private sealed class SafeAreaInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? view, WindowInsetsCompat? insets)
        {
            if (view is null || insets is null)
                return insets ?? WindowInsetsCompat.Consumed;

            var bars = insets.GetInsets(
                WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
            view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            return WindowInsetsCompat.Consumed;
        }
    }
}
