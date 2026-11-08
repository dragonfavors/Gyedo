using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Windows;

namespace Stylet.Samples.CustomStartup;

public partial class App : Application
{
    public IHost Host { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        // 업데이트, 단일 인스턴스 확인, 네이티브 라이브러리 설정 등 앱의 사전 작업 위치입니다.
        var app = new App();
        app.InitializeComponent();
        app.Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureStylet(app, views => views.ViewModelNameSuffix = "VM")
            .ConfigureServices((_, services) => services.AddSingleton<MainVM>())
            .Build();
        return app.Run();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            if (!await this.StartStyletAsync(this.Host))
                return;

            // 폰트·테마 등 첫 화면을 표시하기 전의 UI 초기화 위치입니다.
            this.Host.Services.GetRequiredService<IWindowManager>()
                .ShowWindow(this.Host.Services.GetRequiredService<MainVM>());
        }
        catch (Exception exception)
        {
            Trace.TraceError("앱 시작 실패: {0}", exception);
            await this.ShutdownStyletAsync(1);
        }
    }
}
