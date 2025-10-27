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
    }
}