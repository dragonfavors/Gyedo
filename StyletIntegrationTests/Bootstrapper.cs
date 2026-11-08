using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Stylet;
using Stylet.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StyletIntegrationTests
{
    public class Bootstrapper : Bootstrapper<ShellViewModel>
    {
        protected override void Configure()
        {
            LogManager.Enabled = true;
        }

        protected override async void OnUnhandledException(System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            base.OnUnhandledException(e); // Calling this just to trigger some code coverage
            string message = e.Exception.Message;
            if (e.Exception is TargetInvocationException)
                message = e.Exception.InnerException.Message;
            e.Handled = true;
            try
            {
                await this.Services.GetRequiredService<IWindowManager>().ShowMessageBoxAsync(
                    string.Format("Unhandled Exception: {0}", message), icon: MessageBoxImage.Error);
            }
            catch (Exception exception)
            {
                LogManager.GetLogger(this.GetType()).Error(exception, "오류 대화상자를 표시하지 못했습니다.");
            }
        }
    }
}
