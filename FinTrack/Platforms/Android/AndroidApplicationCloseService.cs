using FinTracker.Services;
using Microsoft.Maui.ApplicationModel;

namespace FinTracker.Platforms.Android;

public sealed class AndroidApplicationCloseService : IApplicationCloseService
{
    public void Close() => Platform.CurrentActivity?.FinishAndRemoveTask();
}
