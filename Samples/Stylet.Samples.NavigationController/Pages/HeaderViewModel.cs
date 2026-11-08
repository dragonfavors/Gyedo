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
    public Task NavigateToPage1Async() => this.navigationController.NavigateToPage1Async();
    [RelayCommand]
    public Task NavigateToPage2Async() => this.navigationController.NavigateToPage2Async("the Header");
}
