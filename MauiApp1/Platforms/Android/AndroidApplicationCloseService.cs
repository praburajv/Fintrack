using MauiApp1.Services;
using Microsoft.Maui.ApplicationModel;

namespace MauiApp1.Platforms.Android;

public sealed class AndroidApplicationCloseService : IApplicationCloseService
{
    public void Close() => Platform.CurrentActivity?.FinishAndRemoveTask();
}
