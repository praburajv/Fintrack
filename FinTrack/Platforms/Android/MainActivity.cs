using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using FinTracker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinTracker
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private Platforms.Android.AndroidTopBarTheme? _topBarTheme;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            if (Microsoft.Maui.Controls.Application.Current is { } application && Window is not null)
                _topBarTheme = new Platforms.Android.AndroidTopBarTheme(this,
                    IPlatformApplication.Current!.Services.GetRequiredService<IThemeService>(), application);
        }

        protected override void OnResume()
        {
            base.OnResume();
            _topBarTheme?.Refresh();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (hasFocus) _topBarTheme?.Refresh();
        }

        public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            _topBarTheme?.Refresh();
        }

        protected override void OnDestroy()
        {
            _topBarTheme?.Dispose();
            _topBarTheme = null;
            base.OnDestroy();
        }

        protected override void OnActivityResult(int requestCode, Android.App.Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            Platforms.Android.AndroidBackupFileExporter.Complete(requestCode, resultCode, data);
        }
    }
}
