using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stylet.Samples.NavigationController.Pages;

public partial class HeaderViewModel : Screen
{
    private readonly INavigationController navigationController;

    public HeaderViewModel(INavigationController navigationController)
    {
        this.navigationController = navigationController ?? throw new ArgumentNullException(nameof(navigationController));
    }

    [RelayCommand]
    public void NavigateToPage1() => this.navigationController.NavigateToPage1();
    [RelayCommand]
    public void NavigateToPage2() => this.navigationController.NavigateToPage2("the Header");
}
