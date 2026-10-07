namespace FinTracker.Services
{
    /// <summary>
    /// Modal Error Handler.
    /// </summary>
    public class ModalErrorHandler : IErrorHandler
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        /// <summary>
        /// Handle error in UI.
        /// </summary>
        /// <param name="ex">Exception.</param>
        public void HandleError(Exception ex)
        {
            if (AppShell.IsClosing)
                return;

            // A database/UI operation can finish while Android is tearing down the
            // current Activity. In that case there is no valid Shell on which to
            // display an error; never let the error-reporting path crash the app.
            if (IsDisposedServiceProviderError(ex))
                return;

            DisplayAlertAsync(ex).FireAndForgetSafeAsync();
        }

        private static bool IsDisposedServiceProviderError(Exception ex)
        {
            for (var current = ex; current is not null; current = current.InnerException)
            {
                if (current is ObjectDisposedException disposed &&
                    disposed.ObjectName?.Contains("IServiceProvider", StringComparison.OrdinalIgnoreCase) == true)
                    return true;

                if (current is InvalidOperationException invalid &&
                    invalid.Message.Contains("IServiceProvider", StringComparison.OrdinalIgnoreCase) &&
                    invalid.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        async Task DisplayAlertAsync(Exception ex)
        {
            var entered = false;
            try
            {
                await _semaphore.WaitAsync();
                entered = true;
                if (Application.Current is null)
                    return;

                if (AppShell.IsClosing)
                    return;

                if (Shell.Current is Shell shell)
                    await shell.DisplayAlertAsync("Error", ex.Message, "OK");
            }
            catch (ObjectDisposedException)
            {
                // The Activity/Shell may be disposed during application close.
            }
            catch (InvalidOperationException error) when (error.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase))
            {
                // MAUI can surface a disposed IServiceProvider while the old
                // Activity is being replaced. There is no UI left to alert.
            }
            finally
            {
                if (entered)
                    _semaphore.Release();
            }
        }
    }
}
