using System;
using System.Windows.Input;

namespace Sammenlaeg.Wpf
{
    public class RelayCommand : ICommand
    {
        private readonly MainWindowViewModel _mainWindowViewModel;
        private readonly Func<MainWindowViewModel, bool> canExecuteEvaluator;
        private readonly Action<MainWindowViewModel> methodToExecute;

        public RelayCommand(Action<MainWindowViewModel> methodToExecute,
            Func<MainWindowViewModel, bool> canExecuteEvaluator,
            MainWindowViewModel mainWindowViewModel)
        {
            this.methodToExecute = methodToExecute;
            this.canExecuteEvaluator = canExecuteEvaluator;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public RelayCommand(Action<MainWindowViewModel> methodToExecute, MainWindowViewModel mainWindowViewModel)
            : this(methodToExecute, null, mainWindowViewModel)
        {
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter)
        {
            if (canExecuteEvaluator == null) return true;

            var result = canExecuteEvaluator(_mainWindowViewModel);
            return result;
        }

        public void Execute(object parameter)
        {
            methodToExecute(_mainWindowViewModel);
        }
    }
}