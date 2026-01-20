using System.Windows.Input;

namespace AstroValleyAssistant.Core.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// A generic command that implements ICommand for use in MVVM.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            // If no canExecute predicate, default to true
            return _canExecute switch
            {
                null => true,
                _ => _canExecute(ConvertParameter(parameter))
            };
        }

        // Execute the action with the converted parameter
        public void Execute(object? parameter) =>
            _execute(ConvertParameter(parameter));

        /// <summary>
        /// Safely converts the object parameter to the generic type T.
        /// </summary>
        private T? ConvertParameter(object? parameter)
        {
            // Handles null for reference types and Nullable<T>
            // Returns default (e.g., 0 for int) for non-nullable value types
            if (parameter == null)

                return default;

            // If the parameter is already the correct type
            if (parameter is T typedParam)
                return typedParam;

            try
            {
                // Handle common XAML-to-ViewModel conversions (e.g., string "123" to int 123)
                var targetType = typeof(T);
                var conversionType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                return (T?)Convert.ChangeType(parameter, conversionType);
            }
            catch (Exception) // Catches InvalidCastException, FormatException, etc.
            {
                // If conversion fails, return the default value for T
                return default;
            }
        }
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Async command without a strongly-typed parameter.
    /// Wraps a Func&lt;object?, Task&gt; and integrates with WPF's ICommand.
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _executeAsync;
        private readonly Predicate<object?>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<object?, Task> executeAsync,
                                 Predicate<object?>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting)
                return false; // optional: disable while running

            return _canExecute == null || _canExecute(parameter);
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();

                await _executeAsync(parameter).ConfigureAwait(false);
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// Async command with a strongly-typed parameter T.
    /// Handles basic object-to-T conversion for XAML bindings.
    /// </summary>
    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T?, Task> _executeAsync;
        private readonly Predicate<T?>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<T?, Task> executeAsync,
                                 Predicate<T?>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting)
                return false; // optional: disable while running

            if (_canExecute == null)
                return true;

            return _canExecute(ConvertParameter(parameter));
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();

                var typedParam = ConvertParameter(parameter);
                await _executeAsync(typedParam).ConfigureAwait(false);
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Safely converts the object parameter to the generic type T.
        /// Mirrors the pattern used in your generic RelayCommand.
        /// </summary>
        private static T? ConvertParameter(object? parameter)
        {
            if (parameter == null)
                return default;

            if (parameter is T typedParam)
                return typedParam;

            try
            {
                var targetType = typeof(T);
                var conversionType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                return (T?)Convert.ChangeType(parameter, conversionType);
            }
            catch
            {
                return default;
            }
        }
    }
}