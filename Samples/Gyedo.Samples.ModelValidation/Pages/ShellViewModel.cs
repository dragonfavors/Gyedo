using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gyedo.Samples.ModelValidation.Pages;

public class ShellViewModel : Conductor<IScreen>
{
    private readonly UserViewModel userViewModel;
    public ShellViewModel(UserViewModel userViewModel)
    {
        this.DisplayName = "Gyedo.Samples.ModelValidation";

        this.userViewModel = userViewModel;
    }
    protected override Task OnInitialActivatedAsync(CancellationToken cancellationToken) => this.ActivateItemAsync(this.userViewModel);
}
