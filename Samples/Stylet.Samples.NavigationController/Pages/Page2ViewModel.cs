using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stylet.Samples.NavigationController.Pages;

public partial class Page2ViewModel : Screen
{
    private readonly INavigationController navigationController;

    private string _initiator;
    public string Initiator
    {
        get => this._initiator;
        set => this.SetProperty(ref this._initiator, value);
    }

    public Page2ViewModel(INavigationController navigationController)
    {
        this.navigationController = navigationController ?? throw new ArgumentNullException(nameof(navigationController));
    }

    [RelayCommand]
    public void NavigateToPage1() => this.navigationController.NavigateToPage1();
}
