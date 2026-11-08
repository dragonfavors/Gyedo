using Microsoft.Extensions.DependencyInjection;

namespace Stylet.Samples.TabNavigation;

public class Bootstrapper : Bootstrapper<ShellViewModel>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<Page1ViewModel>();
        services.AddTransient<Page2ViewModel>();
    }
}
