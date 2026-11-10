using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Gyedo.Samples.HelloDialog;

public partial class Dialog1ViewModel : Screen
{
    public string Name { get; set; }

    public Dialog1ViewModel()
    {
        this.DisplayName = "I'm Dialog 1";
    }

    public async Task Close()
    {
        await this.RequestCloseAsync(null);
    }

    [RelayCommand]
    public async Task Save()
    {
        await this.RequestCloseAsync(true);
    }
}
