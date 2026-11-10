using Company.GyedoApplication1.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gyedo;

namespace Company.GyedoApplication1;

public class Bootstrapper : Bootstrapper<ShellViewModel>
{
    protected override void ConfigureHost(HostApplicationBuilder builder)
    {
        // 설정·로깅·호스트 옵션을 구성합니다.
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        // 서비스와 IHostedService 구현을 등록합니다.
    }
}
