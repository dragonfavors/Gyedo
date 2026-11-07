using Stylet.Examples;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stylet.Samples.NavigationController.Pages;

public class HeaderViewModel : Screen
{
    private DelegateCommand navigateToPage1Command;
    public ICommand NavigateToPage1Command => this.navigateToPage1Command ?? (this.navigateToPage1Command = new DelegateCommand(this, this.NavigateToPage1));

    private DelegateCommand navigateToPage2Command;
    public ICommand NavigateToPage2Command => this.navigateToPage2Command ?? (this.navigateToPage2Command = new DelegateCommand(this, this.NavigateToPage2));

    private readonly INavigationController navigationController;

    public HeaderViewModel(INavigationController navigationController)
    {
        this.navigationController = navigationController ?? throw new ArgumentNullException(nameof(navigationController));
    }

    public void NavigateToPage1() => this.navigationController.NavigateToPage1();
    public void NavigateToPage2() => this.navigationController.NavigateToPage2("the Header");
}
