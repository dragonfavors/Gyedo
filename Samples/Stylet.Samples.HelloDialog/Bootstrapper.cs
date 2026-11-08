using Microsoft.Extensions.DependencyInjection;

namespace Stylet.Samples.HelloDialog;

public class Bootstrapper : Bootstrapper<ShellViewModel>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IDialogFactory, DialogFactory>();
    }

    private sealed class DialogFactory : IDialogFactory
    {
        public Dialog1ViewModel CreateDialog1() => new();
    }
}
