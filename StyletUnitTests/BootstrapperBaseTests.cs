using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Stylet;
using System;
using System.Threading;
using System.Windows;

namespace StyletUnitTests;

[TestFixture, Apartment(ApartmentState.STA)]
public class BootstrapperBaseTests
{
    private class RootViewModel : IDisposable
    {
        public bool DisposeCalled;

        public void Dispose()
        {
            this.DisposeCalled = true;
        }
    }

    private class MyBootstrapperBase : BootstrapperBase
    {
        private readonly IViewManager viewManager;
        private readonly IWindowManager windowManager;

        public MyBootstrapperBase(IViewManager viewManager, IWindowManager windowManager)
        {
            this.viewManager = viewManager;
            this.windowManager = windowManager;


        }

        public new Application Application => base.Application;

        public readonly RootViewModel MyRootViewModel = new();

        public bool GetInstanceCalled;
        public override object GetInstance(Type service)
        {
            this.GetInstanceCalled = true;
            if (service == typeof(IViewManager))
                return this.viewManager;
            if (service == typeof(IWindowManager))
                return this.windowManager;
            return null;
        }

        public bool LaunchCalled;
        protected override Task LaunchAsync()
        {
            this.LaunchCalled = true;
            return Task.CompletedTask;
        }

        public bool OnLaunchCalled;
        protected override void OnLaunch()
        {
            this.OnLaunchCalled = true;
        }

        public bool OnStartCalled;
        protected override void OnStart()
        {
            this.OnStartCalled = true;
        }

        public bool OnExitCalled;
        protected override void OnExit(ExitEventArgs e)
        {
            this.OnExitCalled = true;
        }

        public bool ConfigureBootstrapperCalled;
        protected override void ConfigureBootstrapper()
        {
            this.ConfigureBootstrapperCalled = true;
            base.ConfigureBootstrapper();
        }


        public new async Task DisplayRootViewAsync(object rootViewModel)
        {
            await base.DisplayRootViewAsync(rootViewModel);
        }
    }

    private class FakeDispatcher : IDispatcher
    {
        public void Post(Action action, DispatchPriority priority = DispatchPriority.Lowest)
        {
            throw new NotImplementedException();
        }

        public void Send(Action action, DispatchPriority priority = DispatchPriority.Lowest)
        {
            throw new NotImplementedException();
        }

        public Task InvokeAsync(Action action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<T> InvokeAsync<T>(Func<T> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task InvokeAsync(Func<Task> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<T> InvokeAsync<T>(Func<Task<T>> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public bool IsCurrent => throw new NotImplementedException();
    }

    
    private MyBootstrapperBase bootstrapper;
    private Mock<IViewManager> viewManager;
    private Mock<IWindowManager> windowManager;

    private IDispatcher dispatcher;

    [SetUp]
    public async Task SetUp()
    {
        this.dispatcher = Execute.Dispatcher;
        this.viewManager = new Mock<IViewManager>();
        this.windowManager = new Mock<IWindowManager>();
        this.bootstrapper = new MyBootstrapperBase(this.viewManager.Object, this.windowManager.Object);
        await this.bootstrapper.StartAsync(Array.Empty<string>());
    }

    [TearDown]
    public void TearDown()
    {
        Execute.Dispatcher = this.dispatcher;
    }

    [Test]
    public void SetupThrowsIfApplicationIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => this.bootstrapper.Setup(null));
    }

    [Test]
    public async Task StartCallsConfigureBootstrapper()
    {
        await this.bootstrapper.StartAsync(new string[0]);
        Assert.True(this.bootstrapper.ConfigureBootstrapperCalled);
    }

    [Test]
    public async Task StartAssignsArgs()
    {
        await this.bootstrapper.StartAsync(new[] { "one", "two" });
        Assert.That(this.bootstrapper.Args, Is.EquivalentTo(new[] { "one", "two" }));
    }

    [Test]
    public async Task StartCallsLaunch()
    {
        await this.bootstrapper.StartAsync(new string[0]);
        Assert.True(this.bootstrapper.LaunchCalled);
    }

    [Test]
    public async Task StartCallsOnLaunch()
    {
        await this.bootstrapper.StartAsync(new string[0]);
        Assert.True(this.bootstrapper.OnLaunchCalled);
    }

    [Test]
    public async Task DisplayRootViewDisplaysTheRootView()
    {
        object viewModel = new();
        await this.bootstrapper.DisplayRootViewAsync(viewModel);

        this.windowManager.Verify(x => x.ShowWindowAsync(viewModel));
    }
}
