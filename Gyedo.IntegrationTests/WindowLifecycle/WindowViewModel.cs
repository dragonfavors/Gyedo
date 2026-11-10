using System.Threading;
using Gyedo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Gyedo.IntegrationTests.WindowLifecycle
{
    public class WindowViewModel : Screen
    {
        private readonly IWindowManager windowManager;

        public BindableCollection<string> Log { get; private set; }

        public WindowViewModel(IWindowManager windowManager)
        {
            this.DisplayName = "Window Lifecycle";

            this.windowManager = windowManager;

            this.Log = new BindableCollection<string>();
        }

        protected override Task OnViewLoadedAsync()
        {
            this.Log.Add("View Loaded");
            return Task.CompletedTask;
        }

        protected override Task OnInitialActivatedAsync(CancellationToken cancellationToken)
        {
            this.Log.Add("Initial Activate");
            return Task.CompletedTask;
        }

        protected override Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            this.Log.Add("Activated");
            return Task.CompletedTask;
        }

        protected override Task OnDeactivatedAsync()
        {
            this.Log.Add("Deactivated");
            return Task.CompletedTask;
        }

        protected override async Task OnClosedAsync()
        {
            await this.windowManager.ShowMessageBoxAsync("Closed", "Closed");
        }
    }
}
