using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Gyedo;

/// <summary>앱이 직접 구성한 Generic Host에 Gyedo를 연결합니다.</summary>
public static class GyedoHostingExtensions
{
    private static readonly ConditionalWeakTable<Application, ApplicationBootstrapper> applications = new();

    /// <summary>Host.CreateDefaultBuilder로 구성하는 호스트에 WPF 서비스를 등록합니다.</summary>
    public static IHostBuilder ConfigureGyedo(this IHostBuilder builder, Application application,
        Action<ViewManagerConfig> configureViews = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(application);
        return builder.ConfigureServices((_, services) => ConfigureGyedo(services, application, configureViews));
    }

    /// <summary>Host.CreateApplicationBuilder로 구성하는 호스트에 WPF 서비스를 등록합니다.</summary>
    public static HostApplicationBuilder ConfigureGyedo(this HostApplicationBuilder builder, Application application,
        Action<ViewManagerConfig> configureViews = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(application);
        ConfigureGyedo(builder.Services, application, configureViews);
        return builder;
    }

    private static void ConfigureGyedo(IServiceCollection services, Application application, Action<ViewManagerConfig> configureViews)
    {
        if (services.Any(x => x.ServiceType == typeof(ApplicationRegistration)))
            throw new InvalidOperationException("ConfigureGyedo은 호스트당 한 번만 호출할 수 있습니다.");

        services.AddSingleton(new ApplicationRegistration(application));
        AddDefaultServices(services, application as IWindowManagerConfig ?? new ApplicationWindowManagerConfig(application), sp =>
        {
            var config = new ViewManagerConfig
            {
                ViewFactory = type => sp.GetService(type) ?? ActivatorUtilities.CreateInstance(sp, type),
                ViewAssemblies = new List<System.Reflection.Assembly> { application.GetType().Assembly },
            };
            configureViews?.Invoke(config);
            return config;
        });
        services.Replace(ServiceDescriptor.Singleton<IHostLifetime>(_ =>
            new WpfHostLifetime(application, () => application.ShutdownGyedoAsync(), attach: false)));
    }

    internal static void AddDefaultServices(IServiceCollection services, IWindowManagerConfig windowConfig,
        Func<IServiceProvider, ViewManagerConfig> viewConfig)
    {
        services.TryAddSingleton(viewConfig);
        services.TryAddSingleton<ViewManager>();
        services.TryAddSingleton<IViewManager>(sp => sp.GetRequiredService<ViewManager>());
        services.TryAddSingleton(windowConfig);
        services.TryAddSingleton<IWindowManager, WindowManager>();
        services.TryAddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.TryAddTransient<IMessageBoxViewModel, MessageBoxViewModel>();
        services.TryAddSingleton<Func<IMessageBoxViewModel>>(sp => () => sp.GetRequiredService<IMessageBoxViewModel>());
    }

    /// <summary>
    /// OnStartup에서 호스트 시작과 WPF 연결을 기다립니다. 시작 중 종료 요청이 오면 false를 반환합니다.
    /// 연결한 호스트의 정상 종료·폐기는 Gyedo가 담당하며, 첫 화면은 앱이 직접 표시합니다.
    /// </summary>
    public static async Task<bool> StartGyedoAsync(this Application application, IHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(host);
        application.Dispatcher.VerifyAccess();
        ApplicationRegistration registration = host.Services.GetService<ApplicationRegistration>();
        if (registration == null || !ReferenceEquals(registration.Application, application))
            throw new InvalidOperationException("같은 Application으로 ConfigureGyedo를 호출한 호스트가 필요합니다.");
        if (host.Services.GetRequiredService<IHostLifetime>() is not WpfHostLifetime lifetime)
            throw new InvalidOperationException("ConfigureGyedo의 WPF 호스트 수명 설정이 필요합니다.");

        if (!applications.TryGetValue(application, out ApplicationBootstrapper bootstrapper))
        {
            if (host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested)
                throw new InvalidOperationException("호스트를 직접 시작하지 말고 StartGyedoAsync를 사용하세요.");
            bootstrapper = new ApplicationBootstrapper(application, host);
            applications.Add(application, bootstrapper);
        }
        else if (!ReferenceEquals(bootstrapper.ProvidedHost, host))
        {
            throw new InvalidOperationException("Application에 이미 다른 호스트가 연결되어 있습니다.");
        }

        lifetime.Attach();
        try
        {
            await bootstrapper.StartAsync(Array.Empty<string>(), cancellationToken);
        }
        catch (OperationCanceledException) when (bootstrapper.ApplicationLifetime.ApplicationStopping.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        return !bootstrapper.ApplicationLifetime.ApplicationStopping.IsCancellationRequested;
    }

    /// <summary>연결된 호스트를 정리한 뒤 WPF 애플리케이션을 종료합니다.</summary>
    public static Task ShutdownGyedoAsync(this Application application, int exitCode = 0)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (!applications.TryGetValue(application, out ApplicationBootstrapper bootstrapper))
            throw new InvalidOperationException("StartGyedoAsync로 연결한 호스트가 없습니다.");
        return bootstrapper.ShutdownAsync(exitCode);
    }

    private sealed class ApplicationRegistration(Application application)
    {
        public Application Application { get; } = application;
    }

    private sealed class ApplicationWindowManagerConfig(Application application) : IWindowManagerConfig
    {
        public Window GetActiveWindow() => application.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive)
            ?? application.MainWindow;
    }

    private sealed class ApplicationBootstrapper : HostBootstrapperBase
    {
        public IHost ProvidedHost { get; }
        public IHostApplicationLifetime ApplicationLifetime { get; }

        public ApplicationBootstrapper(Application application, IHost host)
        {
            this.ProvidedHost = host;
            this.ApplicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            this.Setup(application);
        }

        private protected override IHost BuildHost(string[] args) => this.ProvidedHost;

        protected override Task LaunchAsync()
        {
            var source = new Uri("pack://application:,,,/Gyedo.Wpf;component/Xaml/GyedoResourceDictionary.xaml");
            if (!this.Application.Resources.MergedDictionaries.Any(x => x.Source == source))
                this.Application.Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = source });
            return Task.CompletedTask;
        }
    }
}
