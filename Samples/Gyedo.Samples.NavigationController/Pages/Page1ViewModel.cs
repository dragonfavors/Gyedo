using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gyedo.Samples.NavigationController.Pages;

public partial class Page1ViewModel : Screen
{
    private readonly INavigationController navigationController;

    public Page1ViewModel(INavigationController navigationController)
    {
        this.navigationController = navigationController ?? throw new ArgumentNullException(nameof(navigationController));
    }

    [RelayCommand]
    public Task NavigateToPage2Async() => this.navigationController.NavigateToPage2Async("Page 1");
}
