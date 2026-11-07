using System;
using System.ComponentModel;
using System.Windows.Input;

namespace Stylet.Examples
{
    // Example-only ICommand implementation; not part of the Stylet library.
    internal sealed class DelegateCommand : ICommand
    {
        private readonly Action<object> execute;
        private readonly Predicate<object> canExecute;

        public DelegateCommand(INotifyPropertyChanged owner, Action execute, Func<bool> canExecute = null)
            : this(owner, _ => execute(), canExecute == null ? (Predicate<object>)null : _ => canExecute())
        {
        }

        public DelegateCommand(INotifyPropertyChanged owner, Action<object> execute, Predicate<object> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
            if (canExecute != null)
                owner.PropertyChanged += this.OnOwnerPropertyChanged;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => this.canExecute == null || this.canExecute(parameter);

        public void Execute(object parameter)
        {
            if (this.CanExecute(parameter))
                this.execute(parameter);
        }

        private void OnOwnerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Stylet.Execute.OnUIThread(() => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }
    }
}
