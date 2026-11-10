using Gyedo.Samples.NavigationController.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gyedo.Samples.NavigationController;

public interface INavigationController
{
    Task NavigateToPage1Async();
    Task NavigateToPage2Async(string initiator);
}

public interface INavigationControllerDelegate
{
    Task NavigateToAsync(IScreen screen);
}

public class NavigationController : INavigationController
{
    private readonly Func<Page1ViewModel> page1ViewModelFactory;
    private readonly Func<Page2ViewModel> page2ViewModelFactory;

    public INavigationControllerDelegate Delegate { get; set; }

    public NavigationController(Func<Page1ViewModel> page1ViewModelFactory, Func<Page2ViewModel> page2ViewModelFactory)
    {
        this.page1ViewModelFactory = page1ViewModelFactory ?? throw new ArgumentNullException(nameof(page1ViewModelFactory));
        this.page2ViewModelFactory = page2ViewModelFactory ?? throw new ArgumentNullException(nameof(page2ViewModelFactory));
    }

    public async Task NavigateToPage1Async()
    {
        await this.Delegate.NavigateToAsync(this.page1ViewModelFactory());
    }

    public async Task NavigateToPage2Async(string initiator)
    {
        Page2ViewModel vm = this.page2ViewModelFactory();
        vm.Initiator = initiator;
        await this.Delegate.NavigateToAsync(vm);
    }
}
