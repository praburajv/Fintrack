using System.ComponentModel;

namespace MauiApp1.Services;

public interface ICriticalOperationState : INotifyPropertyChanged
{
    bool IsBusy { get; }
    IDisposable Begin();
}
