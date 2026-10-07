using System.ComponentModel;

namespace FinTracker.Services;

public interface ICriticalOperationState : INotifyPropertyChanged
{
    bool IsBusy { get; }
    IDisposable Begin();
}
