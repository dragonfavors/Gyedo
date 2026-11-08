using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stylet;

/// <summary>
/// Generic Host와 Microsoft DI로 루트 ViewModel을 시작합니다.
/// </summary>
/// <remarks>
/// 루트 ViewModel이 필요 없으면 <see cref="HostBootstrapperBase"/>를 사용합니다.
/// </remarks>
/// <typeparam name="TRootViewModel">Type of the root ViewModel. This will be instantiated and displayed</typeparam>
public abstract class Bootstrapper<TRootViewModel> : HostBootstrapperBase where TRootViewModel : class
{
    private TRootViewModel _rootViewModel;

    /// <summary>
    /// Gets the root ViewModel, creating it first if necessary
    /// </summary>
    protected virtual TRootViewModel RootViewModel => this._rootViewModel ??= this.Services.GetRequiredService<TRootViewModel>();

    /// <summary>
    /// Called when the application is launched. Displays the root view.
    /// </summary>
    protected override void Launch()
    {
        this.DisplayRootView(this.RootViewModel);
    }

    /// <summary>
    /// 루트 ViewModel의 생성과 폐기는 호스트에 맡깁니다.
    /// </summary>
    protected override void DefaultConfigureServices(IServiceCollection services)
    {
        base.DefaultConfigureServices(services);
        if (!typeof(TRootViewModel).IsAbstract && !typeof(TRootViewModel).IsInterface)
            services.TryAddSingleton<TRootViewModel>();
    }
}
