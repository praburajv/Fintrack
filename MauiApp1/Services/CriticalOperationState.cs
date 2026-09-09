using System.ComponentModel;

namespace MauiApp1.Services;

public sealed class CriticalOperationState : ICriticalOperationState
{
    private int _activeOperations;

    public bool IsBusy => Volatile.Read(ref _activeOperations) > 0;
    public event PropertyChangedEventHandler? PropertyChanged;

    public IDisposable Begin()
    {
        if (Interlocked.Increment(ref _activeOperations) == 1)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
        return new OperationLease(this);
    }

    private void End()
    {
        if (Interlocked.Decrement(ref _activeOperations) == 0)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
    }

    private sealed class OperationLease(CriticalOperationState owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.End();
        }
    }
}
