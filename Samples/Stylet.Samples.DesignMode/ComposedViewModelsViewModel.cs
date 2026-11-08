using System.Threading;
using System.Threading.Tasks;
using System;

namespace Stylet.Samples.DesignMode;

public class ComposedViewModelsViewModel : Conductor<IScreen>.Collection.OneActive
{
    protected override async Task OnInitialActivatedAsync(CancellationToken cancellationToken)
    {
        await this.ActivateItemAsync(new Screen());
        await this.ActivateItemAsync(new Screen());
    }
}
