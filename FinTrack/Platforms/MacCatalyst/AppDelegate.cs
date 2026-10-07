using Foundation;

namespace FinTracker
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => FinTrackerProgram.CreateMauiApp();
    }
}
