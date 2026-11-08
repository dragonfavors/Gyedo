using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Stylet.Samples.Hello;

public partial class ShellViewModel : Screen
{
    private readonly IWindowManager windowManager;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSayHello))]
    [NotifyCanExecuteChangedFor(nameof(SayHelloCommand))]
    private string name;

    public ShellViewModel(IWindowManager windowManager)
    {
        this.DisplayName = "Hello, Stylet";
        this.windowManager = windowManager;
    }

    public bool CanSayHello => !string.IsNullOrEmpty(this.Name);

    [RelayCommand(CanExecute = nameof(CanSayHello))]
    public async Task SayHello()
    {
        await this.windowManager.ShowMessageBoxAsync($"Hello, {this.Name}");
    }
}
