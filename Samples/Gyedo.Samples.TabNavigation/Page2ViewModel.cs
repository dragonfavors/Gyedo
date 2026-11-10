using System.Threading;
using System.Threading.Tasks;
using System;

namespace Gyedo.Samples.TabNavigation;

public class Page2ViewModel : Screen, IDisposable
{
    public Page2ViewModel()
    {
        this.DisplayName = "Page 2";
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        await base.OnActivatedAsync(cancellationToken);
    }

    protected override async Task OnDeactivatedAsync()
    {
        await base.OnDeactivatedAsync();
    }

    protected override async Task OnClosedAsync()
    {
        await base.OnClosedAsync();
    }

    public void Dispose()
    {
    }
}
