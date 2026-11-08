using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Stylet.Samples.ModelValidation.Pages;

namespace Stylet.Samples.ModelValidation;

public class Bootstrapper : Bootstrapper<ShellViewModel>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<UserViewModel>();
        services.AddTransient(typeof(IModelValidator<>), typeof(FluentModelValidator<>));
        services.AddTransient<IValidator<UserViewModel>, UserViewModelValidator>();
    }
}
