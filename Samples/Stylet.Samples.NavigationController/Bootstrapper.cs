using Microsoft.Extensions.DependencyInjection;
using Stylet.Samples.NavigationController.Pages;
using System;

namespace Stylet.Samples.NavigationController;

public class Bootstrapper : Bootstrapper<ShellViewModel>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<NavigationController>();
        services.AddSingleton<INavigationController>(sp => sp.GetRequiredService<NavigationController>());
        services.AddTransient<HeaderViewModel>();
        services.AddSingleton<Func<Page1ViewModel>>(sp => () => new Page1ViewModel(sp.GetRequiredService<INavigationController>()));
        services.AddSingleton<Func<Page2ViewModel>>(sp => () => new Page2ViewModel(sp.GetRequiredService<INavigationController>()));
    }

    protected override void OnLaunch()
    {
        var navigation = this.Services.GetRequiredService<NavigationController>();
        navigation.Delegate = this.RootViewModel;
        navigation.NavigateToPage1();
    }
}
