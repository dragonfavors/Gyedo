using Microsoft.Extensions.DependencyInjection;

namespace Gyedo.Samples.OverridingViewManager;

public class Bootstrapper : Bootstrapper<ShellViewModel>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IViewManager, CustomViewManager>();
    }
}
