using System.Threading;
using CommunityToolkit.Mvvm.Input;
using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StyletIntegrationTests
{
    public partial class ShellViewModel : Screen
    {
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
            set => this.SetProperty(ref this._showDialogAndDialogResultDialogResult, value);
        }

        [RelayCommand]
        public async Task ShowDialogAndDialogResult()
        {
            var dialog = new ShowDialogAndDialogResult.DialogViewModel();
            this.ShowDialogAndDialogResultDialogResult = await this.windowManager.ShowDialogAsync(dialog);
        }

        [RelayCommand]
        public async Task ShowWindowLifecycle()
        {
            var window = new WindowLifecycle.WindowViewModel(this.windowManager);
            await this.windowManager.ShowWindowAsync(window);
        }

        [RelayCommand]
        public void ThrowException()
        {
            throw new Exception("Hello");
        }

        [RelayCommand]
        public async Task TestDispatcherAsync()
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
                await this.windowManager.ShowMessageBoxAsync("Success", icon: MessageBoxImage.Information);
            else
                await this.windowManager.ShowMessageBoxAsync("Failure");
        }

        [RelayCommand]
        public async Task ShowCommandExecuted()
        {
            await this.windowManager.ShowMessageBoxAsync("Success!");
        }
    }
}
