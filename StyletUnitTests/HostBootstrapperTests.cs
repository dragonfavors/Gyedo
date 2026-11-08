using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Stylet;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StyletUnitTests;

[TestFixture]
public class HostBootstrapperTests
{
    public interface IRootViewModel { }
    public sealed class RootViewModel : IRootViewModel, IDisposable
    {
        public int DisposeCount;
        public void Dispose() => this.DisposeCount++;
    }

    public sealed class ScopedDependency { }
    public sealed class InvalidRoot(ScopedDependency dependency) { public ScopedDependency Dependency = dependency; }
    public sealed class FactoryRoot(IRootViewModel dependency) { public IRootViewModel Dependency = dependency; }

    private sealed class ProbeService : IHostedService, IDisposable
    {
        public Task Starting = Task.CompletedTask;
        public Task Stopping = Task.CompletedTask;
        public int StartCount, StopCount, DisposeCount;
        public Task StartAsync(CancellationToken token) { this.StartCount++; return this.Starting.WaitAsync(token); }
        public Task StopAsync(CancellationToken token) { this.StopCount++; return this.Stopping.WaitAsync(token); }
        public void Dispose() => this.DisposeCount++;
    }

    private sealed class TestBootstrapper<T> : Bootstrapper<T> where T : class
    {
        public Action<IServiceCollection> Register;
        public T LaunchedRoot;
        public int LaunchCount;

        protected override void ConfigureHost(HostApplicationBuilder builder)
        {
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string> { ["Example"] = "configured" });
        }

        protected override void ConfigureServices(IServiceCollection services) => this.Register?.Invoke(services);
        protected override Task LaunchAsync() { this.LaunchedRoot = this.RootViewModel; this.LaunchCount++; return Task.CompletedTask; }
    }

    [Test]
    public async Task RejectedWindowShutdownKeepsHostAliveAndCanBeRetried()
    {
        var windows = new Mock<IWindowManager>();
        windows.SetupSequence(x => x.CloseAllAsync()).ReturnsAsync(false).ReturnsAsync(true);
        await using var bootstrapper = new TestBootstrapper<RootViewModel>
        {
            Register = services => services.AddSingleton(windows.Object),
        };
        await bootstrapper.StartAsync(Array.Empty<string>());
        RootViewModel root = bootstrapper.LaunchedRoot;
        await bootstrapper.ShutdownAsync();
        Assert.That(root.DisposeCount, Is.Zero);
        await bootstrapper.ShutdownAsync();
        Assert.That(root.DisposeCount, Is.EqualTo(1));
        windows.Verify(x => x.CloseAllAsync(), Times.Exactly(2));
    }

    [Test]
    public async Task HostProvidesFrameworkServicesAndConfiguration()
    {
        await using var bootstrapper = new TestBootstrapper<RootViewModel>();
        await bootstrapper.StartAsync(new[] { "--TestArgument=present" });
        IServiceProvider services = bootstrapper.Services;

        Assert.IsInstanceOf<WindowManager>(services.GetRequiredService<IWindowManager>());
        Assert.AreSame(services.GetRequiredService<ViewManager>(), services.GetRequiredService<IViewManager>());
        Assert.AreSame(WeakReferenceMessenger.Default, services.GetRequiredService<IMessenger>());
        Assert.AreEqual("configured", services.GetRequiredService<IConfiguration>()["Example"]);
        Assert.AreEqual("present", services.GetRequiredService<IConfiguration>()["TestArgument"]);
        Assert.NotNull(services.GetRequiredService<ILogger<RootViewModel>>());
        Assert.AreSame(bootstrapper.LaunchedRoot, bootstrapper.GetInstance(typeof(RootViewModel)));
        Assert.IsInstanceOf<MessageBoxView>(bootstrapper.GetInstance(typeof(MessageBoxView)));
    }

    [Test]
    public async Task ExplicitMessengerRegistrationIsPreserved()
    {
        var messenger = new WeakReferenceMessenger();
        await using var bootstrapper = new TestBootstrapper<RootViewModel>
        {
            Register = services => services.AddSingleton<IMessenger>(messenger),
        };
        await bootstrapper.StartAsync(Array.Empty<string>());
        Assert.That(bootstrapper.Services.GetRequiredService<IMessenger>(), Is.SameAs(messenger));
    }

    [Test]
    public async Task MessageBoxFactoryUsesTheRegisteredImplementation()
    {
        var replacement = new Mock<IMessageBoxViewModel>().Object;
        await using var bootstrapper = new TestBootstrapper<RootViewModel>
        {
            Register = services => services.AddTransient<IMessageBoxViewModel>(_ => replacement),
        };
        await bootstrapper.StartAsync(Array.Empty<string>());

        Assert.AreSame(replacement, bootstrapper.Services.GetRequiredService<Func<IMessageBoxViewModel>>()());
    }

    [Test]
    public async Task StartupWaitsForHostedServicesAndOnlyLaunchesOnce()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new ProbeService { Starting = gate.Task };
        await using var bootstrapper = new TestBootstrapper<RootViewModel>
        {
            Register = services => services.AddSingleton<IHostedService>(_ => service),
        };
        Task start = bootstrapper.StartAsync(Array.Empty<string>());
        Assert.IsFalse(start.IsCompleted);
        Assert.IsNull(bootstrapper.LaunchedRoot);
        Assert.AreSame(start, bootstrapper.StartAsync(Array.Empty<string>()));

        gate.SetResult();
        await start.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, service.StartCount);
        Assert.AreEqual(1, bootstrapper.LaunchCount);
    }

    [Test]
    public async Task ShutdownWaitsAndDisposesContainerOwnedObjectsOnce()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new ProbeService { Stopping = gate.Task };
        var bootstrapper = new TestBootstrapper<RootViewModel>
        {
            Register = services => services.AddSingleton<IHostedService>(_ => service),
        };
        await bootstrapper.StartAsync(Array.Empty<string>());
        RootViewModel root = bootstrapper.LaunchedRoot;
        Task stop = bootstrapper.StopAsync();
        Assert.IsFalse(stop.IsCompleted);
        Assert.AreEqual(0, root.DisposeCount);
        Assert.AreSame(stop, bootstrapper.StopAsync());

        gate.SetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        await bootstrapper.DisposeAsync();
        bootstrapper.Dispose();
        Assert.AreEqual(1, service.StopCount);
        Assert.AreEqual(1, service.DisposeCount);
        Assert.AreEqual(1, root.DisposeCount);
    }

    [Test]
    public async Task ExternallyRegisteredInstancesRemainCallerOwned()
    {
        var root = new RootViewModel();
        var bootstrapper = new TestBootstrapper<RootViewModel> { Register = services => services.AddSingleton(root) };
        await bootstrapper.StartAsync(Array.Empty<string>());
        await bootstrapper.StopAsync();
        Assert.AreSame(root, bootstrapper.LaunchedRoot);
        Assert.AreEqual(0, root.DisposeCount);
        root.Dispose();
    }

    [Test]
    public async Task ExplicitRootFactoryDoesNotRequireUnusedConstructorDependencies()
    {
        await using var bootstrapper = new TestBootstrapper<FactoryRoot>
        {
            Register = services => services.AddSingleton(_ => new FactoryRoot(null)),
        };
        await bootstrapper.StartAsync(Array.Empty<string>());
        Assert.NotNull(bootstrapper.LaunchedRoot);
    }

    [Test]
    public async Task InterfaceRootUsesTheRegisteredImplementation()
    {
        await using var bootstrapper = new TestBootstrapper<IRootViewModel>
        {
            Register = services => services.AddSingleton<IRootViewModel, RootViewModel>(),
        };
        await bootstrapper.StartAsync(Array.Empty<string>());
        Assert.IsInstanceOf<RootViewModel>(bootstrapper.LaunchedRoot);
    }

    [Test]
    public async Task StartupFailureDisposesServicesWithoutLaunching()
    {
        var service = new ProbeService { Starting = Task.FromException(new InvalidOperationException("startup failed")) };
        var bootstrapper = new TestBootstrapper<RootViewModel>
        {
            Register = services => services.AddSingleton<IHostedService>(_ => service),
        };
        Assert.ThrowsAsync<InvalidOperationException>(() => bootstrapper.StartAsync(Array.Empty<string>()));
        await bootstrapper.StopAsync();
        Assert.IsNull(bootstrapper.LaunchedRoot);
        Assert.AreEqual(1, service.DisposeCount);
    }

    [Test]
    public async Task StoppingDuringStartupDoesNotLaunchTheWindow()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new ProbeService { Starting = gate.Task };
        var bootstrapper = new TestBootstrapper<RootViewModel>
        {
            Register = services => services.AddSingleton<IHostedService>(_ => service),
        };
        Task start = bootstrapper.StartAsync(Array.Empty<string>());
        Task stop = bootstrapper.StopAsync();
        gate.SetResult();
        await Task.WhenAll(start, stop).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsNull(bootstrapper.LaunchedRoot);
        Assert.AreEqual(1, service.StopCount);
        Assert.AreEqual(1, service.DisposeCount);
    }

    [Test]
    public async Task InvalidSingletonScopedDependencyIsRejected()
    {
        var bootstrapper = new TestBootstrapper<InvalidRoot>
        {
            Register = services => services.AddScoped<ScopedDependency>(),
        };
        Assert.ThrowsAsync<AggregateException>(() => bootstrapper.StartAsync(Array.Empty<string>()));
        await bootstrapper.DisposeAsync();
        Assert.IsNull(bootstrapper.LaunchedRoot);
    }
}
