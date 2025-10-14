using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AstroValleyAssistant.Core
{
    public class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Raised when a property's value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Sets the value of a property and raises the PropertyChanged event if the value changes.
        /// </summary>
        protected void Set<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
                return;

            storage = value;
            OnPropertyChanged(propertyName ?? string.Empty); // Ensure propertyName is never null
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Ensures proper disposal of resources safely.
        /// </summary>
        protected virtual void DisposeResources() { }

        /// <summary>
        /// Child classes can override this method to perform.
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (_disposed)
                return; // Prevent multiple disposals

            try
            {
                if (disposing)
                {
                    // Dispose managed resources safely
                    DisposeResources();
                }

                // Free unmanaged resources (if any)
                _disposed = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dispose Error: {ex.Message}");
            }
        }

        #endregion
    }
}
