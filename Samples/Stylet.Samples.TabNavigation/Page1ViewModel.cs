using System.Threading;
using System.Threading.Tasks;
using System;

namespace Stylet.Samples.TabNavigation;

public class Page1ViewModel : Screen, IDisposable
{
    public Page1ViewModel()
    {
        this.DisplayName = "Page 1";
    }

    protected override async Task OnClosedAsync()
    {
        await base.OnClosedAsync();
    }

    public void Dispose()
    {

    }
}
