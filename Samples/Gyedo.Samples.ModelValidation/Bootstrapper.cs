using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Gyedo.Samples.ModelValidation.Pages;

namespace Gyedo.Samples.ModelValidation;

public class Bootstrapper : Bootstrapper<ShellViewModel>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<UserViewModel>();
        services.AddTransient(typeof(IModelValidator<>), typeof(FluentModelValidator<>));
        services.AddTransient<IValidator<UserViewModel>, UserViewModelValidator>();
    }
}
