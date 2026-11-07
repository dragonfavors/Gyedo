using Stylet.Examples;
using System.Windows.Input;
using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StyletIntegrationTests
{
    public class ShellViewModel : Screen
    {
        private DelegateCommand showDialogAndDialogResultCommand;
        public ICommand ShowDialogAndDialogResultCommand => this.showDialogAndDialogResultCommand ?? (this.showDialogAndDialogResultCommand = new DelegateCommand(this, this.ShowDialogAndDialogResult));

        private DelegateCommand showWindowLifecycleCommand;
        public ICommand ShowWindowLifecycleCommand => this.showWindowLifecycleCommand ?? (this.showWindowLifecycleCommand = new DelegateCommand(this, this.ShowWindowLifecycle));

        private DelegateCommand throwExceptionCommand;
        public ICommand ThrowExceptionCommand => this.throwExceptionCommand ?? (this.throwExceptionCommand = new DelegateCommand(this, this.ThrowException));

        private DelegateCommand testDispatcherCommand;
        public ICommand TestDispatcherCommand => this.testDispatcherCommand ?? (this.testDispatcherCommand = new DelegateCommand(this, this.TestDispatcher));

        private DelegateCommand showCommandExecutedCommand;
        public ICommand ShowCommandExecutedCommand => this.showCommandExecutedCommand ?? (this.showCommandExecutedCommand = new DelegateCommand(this, this.ShowCommandExecuted));

        private readonly IWindowManager windowManager;

        public ShellViewModel(IWindowManager windowManager)
        {
            this.windowManager = windowManager;

            this.DisplayName = "ShellViewModel";
        }



        private bool? _showDialogAndDialogResultDialogResult;
        public bool? ShowDialogAndDialogResultDialogResult
        {
            get => this._showDialogAndDialogResultDialogResult;
            set => this.SetAndNotify(ref this._showDialogAndDialogResultDialogResult, value);
        }

        public void ShowDialogAndDialogResult()
        {
            var dialog = new ShowDialogAndDialogResult.DialogViewModel();
            this.ShowDialogAndDialogResultDialogResult = this.windowManager.ShowDialog(dialog);
        }

        public void ShowWindowLifecycle()
        {
            var window = new WindowLifecycle.WindowViewModel(this.windowManager);
            this.windowManager.ShowWindow(window);
        }

        public void ThrowException()
        {
            throw new Exception("Hello");
        }

        public async void TestDispatcher()
        {
            IDispatcher dispatcher = Execute.Dispatcher;
            var log = new List<string>();

            await Task.Run(() => dispatcher.Send(() => { lock(log) { log.Add("One"); }; }));
            lock (log) { log.Add("Two"); };

            await Task.Run(() => dispatcher.Post(() => { lock (log) { log.Add("Three"); }; }));
            lock (log) { log.Add("Four"); };

            // OK, so at this point there's a queued message saying to add Three to the log
            // Give the main thread time to process that message
            await Task.Delay(100);

            if (log.SequenceEqual(new[] { "One", "Two", "Four", "Three" }))
                this.windowManager.ShowMessageBox("Success", icon: MessageBoxImage.Information);
            else
                this.windowManager.ShowMessageBox("Failure");
        }

        public void ShowCommandExecuted()
        {
            this.windowManager.ShowMessageBox("Success!");
        }
    }
}
