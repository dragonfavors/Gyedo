using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;

namespace Stylet.Samples.CustomStartup;

public partial class MainVM : Screen
{
    public string Message { get; }

    public MainVM(IConfiguration configuration)
    {
        this.DisplayName = "Custom Main + Generic Host";
        this.Message = configuration["Greeting"] ?? "Main에서 직접 구성한 호스트로 실행했습니다.";
    }

    [RelayCommand]
    private void Close() => this.RequestClose();
}
