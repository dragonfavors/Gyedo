using System.Threading;
using System.Threading.Tasks;
namespace Gyedo.Samples.TabNavigation;
public class ShellViewModel : Conductor<IScreen>.Collection.OneActive
{
    private readonly Page1ViewModel page1;
    private readonly Page2ViewModel page2;
    public ShellViewModel(Page1ViewModel page1, Page2ViewModel page2)
    {
        this.page1 = page1;
        this.page2 = page2;
    }
    protected override async Task OnInitialActivatedAsync(CancellationToken cancellationToken)
    {
        await this.AddItemAsync(this.page1);
        await this.AddItemAsync(this.page2);
        await this.ActivateItemAsync(this.page1);
    }
}
