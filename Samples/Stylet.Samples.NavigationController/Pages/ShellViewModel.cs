using System.Threading;
using System.Threading.Tasks;
using System;
using Stylet;

namespace Stylet.Samples.NavigationController.Pages;

public class ShellViewModel : Conductor<IScreen>, INavigationControllerDelegate
{
    public HeaderViewModel HeaderViewModel { get; }

    public ShellViewModel(HeaderViewModel headerViewModel)
    {
        this.HeaderViewModel = headerViewModel ?? throw new ArgumentNullException(nameof(headerViewModel));
    }

    public async Task NavigateToAsync(IScreen screen)
    {
        await this.ActivateItemAsync(screen);
    }
}
