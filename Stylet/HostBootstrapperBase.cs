using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stylet.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Stylet;

/// <summary>WPF 시작·종료를 Generic Host의 비동기 생명주기에 연결합니다.</summary>
public abstract class HostBootstrapperBase : BootstrapperBase, IAsyncDisposable
{
    private readonly object lifecycleLock = new();
    private Task startTask;
    private Task stopTask;
    private Task shutdownTask;
    private WpfHostLifetime lifetime;
    private CancellationTokenRegistration stoppingRegistration;
    private int stopping;
    private int disposed;

    /// <summary>애플리케이션의 호스트입니다. 시작 전에는 null입니다.</summary>
    public IHost Host { get; private set; }

    /// <summary>호스트의 서비스 공급자입니다.</summary>
    public IServiceProvider Services => this.Host?.Services
        ?? throw new InvalidOperationException("호스트가 아직 시작되지 않았습니다.");

    /// <summary>설정·로깅·서비스를 구성할 호스트 빌더를 생성합니다.</summary>
    protected virtual HostApplicationBuilder CreateHostBuilder(string[] args) =>
        Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

    /// <summary>설정, 로깅, 호스트 옵션을 구성합니다.</summary>
    protected virtual void ConfigureHost(HostApplicationBuilder builder) { }

    /// <summary>애플리케이션 서비스를 등록합니다.</summary>
    protected virtual void ConfigureServices(IServiceCollection services) { }

    /// <summary>WPF 화면 관리에 필요한 기본 서비스를 등록합니다.</summary>
    protected virtual void DefaultConfigureServices(IServiceCollection services)
    {
        StyletHostingExtensions.AddDefaultServices(services, this, _ => new ViewManagerConfig
        {
            ViewFactory = this.GetInstance,
            ViewAssemblies = new List<Assembly> { this.GetType().Assembly },
        });
    }

    /// <summary>기본 호스트를 구성하거나 외부에서 구성한 호스트를 제공합니다.</summary>
    private protected virtual IHost BuildHost(string[] args)
    {
        HostApplicationBuilder builder = this.CreateHostBuilder(args);
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }));
        this.lifetime = new WpfHostLifetime(this.Application, () => this.ShutdownAsync());
        builder.Services.AddSingleton<IHostLifetime>(this.lifetime);
        this.ConfigureHost(builder);
        this.ConfigureServices(builder.Services);
        this.DefaultConfigureServices(builder.Services);
        return builder.Build();
    }

    /// <summary>WPF Startup 이벤트용 진입점입니다. 직접 호출할 때는 StartAsync를 사용하세요.</summary>
    public override async void Start(string[] args)
    {
        try { await this.StartAsync(args); }
        catch (Exception exception)
        {
            Trace.TraceError("호스트 시작 실패: {0}", exception);
            this.Application?.Shutdown(1);
            throw;
        }
    }

    /// <summary>서비스 시작을 기다린 뒤 루트 화면을 표시합니다.</summary>
    public Task StartAsync(string[] args, CancellationToken cancellationToken = default)
    {
        lock (this.lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(this.disposed != 0, this);
            return this.startTask ??= this.StartCoreAsync(args, cancellationToken);
        }
    }

    private async Task StartCoreAsync(string[] args, CancellationToken cancellationToken)
    {
        this.Args = args ?? Array.Empty<string>();
        this.OnStart();
        try
        {
            this.Host = this.BuildHost(this.Args);

            IHostApplicationLifetime applicationLifetime = this.Services.GetRequiredService<IHostApplicationLifetime>();
            this.stoppingRegistration = applicationLifetime.ApplicationStopping.Register(this.OnHostStopping);
            await this.Host.StartAsync(cancellationToken);
            if (Volatile.Read(ref this.stopping) != 0 || applicationLifetime.ApplicationStopping.IsCancellationRequested)
                return;

            this.Application?.Resources.Add(View.ViewManagerResourceKey, this.GetInstance(typeof(IViewManager)));
            this.Configure();
            this.Launch();
            this.OnLaunch();
        }
        catch
        {
            await this.StopAndDisposeHostAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>등록된 서비스를 조회하고 미등록 구체 타입은 생성자 주입으로 생성합니다.</summary>
    /// <remarks>직접 생성한 객체의 폐기는 호출자가 담당합니다.</remarks>
    public override object GetInstance(Type type) =>
        this.Services.GetService(type) ?? ActivatorUtilities.CreateInstance(this.Services, type);

    private void OnHostStopping()
    {
        if (Volatile.Read(ref this.stopping) != 0 || this.Application == null || this.Application.Dispatcher.HasShutdownStarted)
            return;
        this.Application.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try { await this.ShutdownAsync(); }
            catch (Exception exception) { Trace.TraceError("호스트 종료 실패: {0}", exception); }
        }));
    }

    /// <summary>서비스 종료와 호스트 자원 해제를 기다립니다.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (this.lifecycleLock)
            return this.stopTask ??= this.StopCoreAsync(cancellationToken);
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref this.stopping, 1);
        if (this.startTask != null)
        {
            try { await this.startTask; }
            catch { return; } // 시작 실패 경로에서 이미 정리했습니다.
        }
        await this.StopAndDisposeHostAsync(cancellationToken);
    }

    private async Task StopAndDisposeHostAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref this.stopping, 1);
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            return;

        this.stoppingRegistration.Dispose();
        try
        {
            if (this.Host != null)
                await this.Host.StopAsync(cancellationToken);
        }
        finally
        {
            try
            {
                if (this.Host is IAsyncDisposable asyncHost)
                    await asyncHost.DisposeAsync();
                else
                    this.Host?.Dispose();
            }
            finally { this.lifetime?.Dispose(); }
        }
    }

    /// <summary>호스트를 먼저 종료한 뒤 WPF 애플리케이션을 종료합니다.</summary>
    public Task ShutdownAsync(int exitCode = 0)
    {
        lock (this.lifecycleLock)
            return this.shutdownTask ??= this.ShutdownCoreAsync(exitCode);
    }

    private async Task ShutdownCoreAsync(int exitCode)
    {
        try { await this.StopAsync(); }
        catch
        {
            if (exitCode == 0)
                exitCode = 1;
            throw;
        }
        finally
        {
            if (this.Application != null && !this.Application.Dispatcher.HasShutdownStarted)
                await this.Application.Dispatcher.InvokeAsync(() => this.Application.Shutdown(exitCode));
        }
    }

    /// <summary>강제 종료 시 자원을 해제합니다. 정상 종료는 StopAsync 또는 DisposeAsync를 사용하세요.</summary>
    public override void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            return;
        this.stoppingRegistration.Dispose();
        try { this.Host?.Dispose(); }
        finally { this.lifetime?.Dispose(); }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await this.StopAsync();
}
