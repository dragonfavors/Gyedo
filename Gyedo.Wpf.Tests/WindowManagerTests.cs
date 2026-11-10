using Moq;
using NUnit.Framework;
using Gyedo;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Gyedo.Wpf.Tests;

[TestFixture, Apartment(ApartmentState.STA)]
public class WindowManagerTests
{
    public interface IMyScreen : IScreen, IDisposable
    { }

    private class TestException : Exception { }

    private class MyWindowManager : WindowManager
    {
        public MyWindowManager(IViewManager viewManager, Func<IMessageBoxViewModel> messageBoxViewModelFactory, IWindowManagerConfig config)
            : base(viewManager, messageBoxViewModelFactory, config) { }

        public new async Task<Window> CreateWindowAsync(object viewModel, bool isDialog, object ownerViewModel = null)
        {
            return await base.CreateWindowAsync(viewModel, isDialog, ownerViewModel);
        }
    }

    private class WindowManagerWithoutCreateWindow : WindowManager
    {
        public WindowManagerWithoutCreateWindow(IViewManager viewManager, Func<IMessageBoxViewModel> messageBoxViewModelFactory, IWindowManagerConfig config)
            : base(viewManager, messageBoxViewModelFactory, config) { }

        protected override Task<Window> CreateWindowAsync(object viewModel, bool isDialog, object ownerViewModel)
        {
            throw new TestException(); // ABORT! ABORT!
        }
    }

    private class MyWindow : Window
    {
        public new void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
        }

        public bool OnClosedCalled;
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            this.OnClosedCalled = true;
        }

        public new void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
        }
    }

    private Mock<IViewManager> viewManager;
    private Mock<IMessageBoxViewModel> messageBoxViewModel;
    private Mock<IWindowManagerConfig> config;
    private MyWindowManager windowManager;
    private SynchronizationContext synchronizationContext;

    [SetUp]
    public void SetUp()
    {
        this.synchronizationContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        this.viewManager = new Mock<IViewManager>();
        this.messageBoxViewModel = new Mock<IMessageBoxViewModel>();
        this.config = new Mock<IWindowManagerConfig>();
        this.windowManager = new MyWindowManager(this.viewManager.Object, () => this.messageBoxViewModel.Object, this.config.Object);
    }

    [TearDown]
    public void RestoreSynchronizationContext() => SynchronizationContext.SetSynchronizationContext(this.synchronizationContext);

    [Test]
    public async Task CreateWindowAsksViewManagerForView()
    {
        object model = new();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Verifiable();
        // Don't care if this throws - that's OK
        try { await this.windowManager.CreateWindowAsync(model, false);  }
        catch (Exception) { }
        this.viewManager.VerifyAll();
    }

    [Test]
    public async Task CreateWindowThrowsIfViewIsntAWindow()
    {
        object model = new();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(new UIElement());
        Assert.ThrowsAsync<GyedoInvalidViewTypeException>(async () => await this.windowManager.CreateWindowAsync(model, false));
    }

    [Test]
    public async Task CreateWindowSetsUpTitleBindingIfViewModelIsIHaveDisplayName()
    {
        var model = new Screen();
        var window = new Window();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);
        
        await this.windowManager.CreateWindowAsync(model, false);

        BindingExpression e = window.GetBindingExpression(Window.TitleProperty);
        Assert.NotNull(e);
        Assert.AreEqual(BindingMode.TwoWay, e.ParentBinding.Mode);
        Assert.AreEqual("DisplayName", e.ParentBinding.Path.Path);
    }

    [Test]
    public async Task CreateWindowDoesNotSetUpTitleBindingIfTitleHasAValueAlready()
    {
        var model = new Screen();
        var window = new Window();
        window.Title = "Foo";
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);

        await this.windowManager.CreateWindowAsync(model, false);

        BindingExpression e = window.GetBindingExpression(Window.TitleProperty);
        Assert.IsNull(e);
        Assert.AreEqual("Foo", window.Title);
    }

    [Test]
    public async Task CreateWindowDoesNotSetUpTitleBindingIfTitleHasABindingAlready()
    {
        var model = new Screen();
        var window = new Window();
        var binding = new Binding("Test") { Mode = BindingMode.TwoWay };
        window.SetBinding(Window.TitleProperty, binding);
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);

        await this.windowManager.CreateWindowAsync(model, false);

        BindingExpression e = window.GetBindingExpression(Window.TitleProperty);
        Assert.AreEqual("Test", e.ParentBinding.Path.Path);
    }

    [Test]
    public async Task CreateWindowDoesSetUpTitleBindingIfTitleIsNameOfTheClass()
    {
        var model = new Screen();
        var window = new Window();
        window.Title = "Window";
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);

        await this.windowManager.CreateWindowAsync(model, false);

        BindingExpression e = window.GetBindingExpression(Window.TitleProperty);
        Assert.AreEqual("DisplayName", e.ParentBinding.Path.Path);
    }

    [Test]
    public async Task CreateWindowActivatesViewModel()
    {
        var model = new Mock<IScreen>();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(new Window());
        await this.windowManager.CreateWindowAsync(model.Object, false);
        model.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    private sealed class LoadingScreen : Screen
    {
        public CancellationToken LoadingToken;

        protected override Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            this.LoadingToken = cancellationToken;
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class ClosingScreen : Screen
    {
        public readonly TaskCompletionSource Cleanup = new();
        public int CloseCalls;
        protected override Task OnClosedAsync()
        {
            this.CloseCalls++;
            return this.Cleanup.Task;
        }
    }

    [Test]
    public async Task PhysicalWindowCloseWaitsForCleanupAndCoalescesRepeatedRequests()
    {
        var model = new ClosingScreen();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);
        await this.windowManager.CreateWindowAsync(model, false);
        Task<bool> first = model.RequestCloseAsync();
        Task<bool> second = model.RequestCloseAsync();
        Assert.That(first, Is.SameAs(second));
        Assert.That(window.OnClosedCalled, Is.False);
        Assert.That(model.ScreenState, Is.EqualTo(ScreenState.Closed));
        model.Cleanup.SetResult();
        Assert.That(await first.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(window.OnClosedCalled, Is.True);
        Assert.That(model.CloseCalls, Is.EqualTo(1));
        Assert.That(model.Parent, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task WindowCloseAndMinimizeCancelAsyncActivation(bool minimize)
    {
        var model = new LoadingScreen();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);
        Task<Window> opening = this.windowManager.CreateWindowAsync(model, false);
        Assert.That(opening.IsCompleted, Is.False);
        if (minimize)
        {
            window.WindowState = WindowState.Minimized;
            window.OnStateChanged(EventArgs.Empty);
        }
        else
        {
            window.Close();
        }
        Assert.That(model.LoadingToken.IsCancellationRequested, Is.True);
        Assert.CatchAsync<OperationCanceledException>(async () => await opening.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(model.ScreenState, Is.EqualTo(ScreenState.Closed));
    }

    [Test]
    public async Task WindowStateChangedActivatesIfMaximized()
    {
        var model = new Mock<IScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        await this.windowManager.CreateWindowAsync(model.Object, false);
        window.WindowState = WindowState.Maximized;
        window.OnStateChanged(EventArgs.Empty);
        model.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task WindowStateChangedActivatesIfNormal()
    {
        var model = new Mock<IScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        await this.windowManager.CreateWindowAsync(model.Object, false);
        window.WindowState = WindowState.Normal;
        window.OnStateChanged(EventArgs.Empty);
        model.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task WindowStateChangedDeactivatesIfMinimized()
    {
        var model = new Mock<IScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        await this.windowManager.CreateWindowAsync(model.Object, false);
        window.WindowState = WindowState.Minimized;
        window.OnStateChanged(EventArgs.Empty);
        model.Verify(x => x.DeactivateAsync());
    }

    [Test]
    public async Task WindowClosingDoesNothingIfAlreadyCancelled()
    {
        var model = new Screen();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);
        await this.windowManager.CreateWindowAsync(model, false);
        window.OnClosing(new CancelEventArgs(true));
    }

    [Test]
    public async Task WindowClosingCancelsIfCanCloseAsyncReturnsSynchronousFalse()
    {
        var model = new Mock<IScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        await this.windowManager.CreateWindowAsync(model.Object, false);
        model.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(false));
        var ea = new CancelEventArgs();
        window.OnClosing(ea);
        Assert.True(ea.Cancel);
    }

    [Test]
    public async Task WindowClosingDefersPhysicalCloseEvenWhenGuardCompletesSynchronously()
    {
        var model = new Mock<IScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        await this.windowManager.CreateWindowAsync(model.Object, false);
        model.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.SetResult();
        var ea = new CancelEventArgs();
        window.OnClosing(ea);
        Assert.True(ea.Cancel);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(window.OnClosedCalled);
    }

    [Test]
    public async Task WindowClosingCancelsIfCanCloseAsyncReturnsAsynchronousFalse()
    {
        var model = new Mock<IScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        await this.windowManager.CreateWindowAsync(model.Object, false);
        model.Setup(x => x.CanCloseAsync()).Returns(Task.Delay(10).ContinueWith(t => false));
        var ea = new CancelEventArgs();
        window.OnClosing(ea);
        Assert.True(ea.Cancel);
    }

    [Test]
    public async Task WindowClosingCancelsIfCanCloseAsyncReturnsAsynchronous()
    {
        var model = new Mock<IScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        await this.windowManager.CreateWindowAsync(model.Object, false);
        var tcs = new TaskCompletionSource<bool>();
        model.Setup(x => x.CanCloseAsync()).Returns(tcs.Task);
        var ea = new CancelEventArgs();
        window.OnClosing(ea);
        Assert.True(ea.Cancel);
    }

    [Test]
    public async Task WindowClosingClosesWindowIfCanCloseAsyncCompletesTrue()
    {
        var model = new Mock<IMyScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        await this.windowManager.CreateWindowAsync(model.Object, false);
        var tcs = new TaskCompletionSource<bool>();
        model.Setup(x => x.CanCloseAsync()).Returns(tcs.Task);
        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.SetResult();
        window.OnClosing(new CancelEventArgs());
        model.Verify(x => x.CloseAsync(), Times.Never);
        tcs.SetResult(true);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        model.Verify(x => x.CloseAsync(), Times.Once);

        Assert.True(window.OnClosedCalled);

        // Check it didn't call WindowClosing again - just the first time
        model.Verify(x => x.CanCloseAsync(), Times.Once);
    }

    [Test]
    public async Task CloseItemDoesNothingIfItemIsWrong()
    {
        var model = new Screen();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);
        await this.windowManager.CreateWindowAsync(model, false);
        await ((IChildDelegate)model.Parent).CloseItemAsync(new object());
    }

    [Test]
    public async Task CloseItemDoesNothingIfCanCloseReturnsFalse()
    {
        var model = new Mock<IScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        object parent = null;
        model.SetupSet(x => x.Parent = It.IsAny<object>()).Callback((object x) => parent = x);
        await this.windowManager.CreateWindowAsync(model.Object, false);

        model.Setup(x => x.CanCloseAsync()).Returns(Task.Delay(10).ContinueWith(t => false));
        await ((IChildDelegate)parent).CloseItemAsync(model.Object);
    }

    [Test]
    public async Task CloseItemClosesAndWindowViewModelIfCanCloseReturnsTrue()
    {
        var model = new Mock<IMyScreen>();
        var window = new MyWindow();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model.Object)).Returns(window);
        object parent = null;
        model.SetupSet(x => x.Parent = It.IsAny<object>()).Callback((object x) => parent = x);
        await this.windowManager.CreateWindowAsync(model.Object, true);

        model.Setup(x => x.CanCloseAsync()).ReturnsAsync(true);
        await ((IChildDelegate)parent).CloseItemAsync(model.Object);

        model.Verify(x => x.CloseAsync());
        Assert.True(window.OnClosedCalled);
    }

    [Test]
    public async Task ShowMessageBoxShowsMessageBox()
    {
        var wm = new WindowManagerWithoutCreateWindow(this.viewManager.Object, () => this.messageBoxViewModel.Object, this.config.Object);

        try { await wm.ShowMessageBoxAsync("text", "title", MessageBoxButton.OKCancel, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxResult.Cancel, null, FlowDirection.RightToLeft, TextAlignment.Right); }
        catch (TestException) { }

        this.messageBoxViewModel.Verify(x => x.Setup("text", "title", MessageBoxButton.OKCancel, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxResult.Cancel, null, FlowDirection.RightToLeft, TextAlignment.Right));
    }

    [Test]
    public async Task CreateWindowSetsWindowStartupLocationToCenterScreenIfThereIsNoOwnerAndItHasNotBeenSetAlready()
    {
        object model = new();
        var window = new Window();
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);

        await this.windowManager.CreateWindowAsync(model, false);

        Assert.AreEqual(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
    }

    [Test]
    public async Task CreateWindowDoesNotSetStartupLocationIfItIsNotManual()
    {
        object model = new();
        var window = new Window();
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);

        await this.windowManager.CreateWindowAsync(model, false);

        Assert.AreEqual(WindowStartupLocation.CenterOwner, window.WindowStartupLocation);
    }

    [Test]
    public async Task CreateWindowDoesNotSetStartupLocationIfLeftSet()
    {
        object model = new();
        var window = new Window();
        window.Left = 1;
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);

        await this.windowManager.CreateWindowAsync(model, false);

        Assert.AreEqual(WindowStartupLocation.Manual, window.WindowStartupLocation);
    }

    [Test]
    public async Task CreateWindowDoesNotSetStartupLocationIfTopSet()
    {
        object model = new();
        var window = new Window();
        window.Top = 1;
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);

        await this.windowManager.CreateWindowAsync(model, false);

        Assert.AreEqual(WindowStartupLocation.Manual, window.WindowStartupLocation);
    }

    [Test]
    public async Task CreateWindowSetsOwnerIfAvailable()
    {
        // We can't actually set the window successfully, since it'll throw an InvalidOperationException
        // ("can't set owner to a window which has not been shown previously")

        object model = new();
        var window = new Window();
        var activeWindow = new Window();

        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(model)).Returns(window);
        this.config.Setup(x => x.GetActiveWindow()).Returns(activeWindow).Verifiable();

        await this.windowManager.CreateWindowAsync(model, true);

        this.config.VerifyAll();
    }

    [Test]
    public async Task CreateWindowFindsAnExplicitScreenOwnerThroughTheViewManager()
    {
        var ownerModel = new Screen();
        var childModel = new Screen();
        var owner = new Window();
        var child = new Window();
        this.viewManager.Setup(x => x.GetView(ownerModel)).Returns(owner);
        this.viewManager.Setup(x => x.CreateAndBindViewForModelIfNecessary(childModel)).Returns(child);

        try
        {
            new WindowInteropHelper(owner).EnsureHandle();
            await this.windowManager.CreateWindowAsync(childModel, true, ownerModel);

            Assert.AreSame(owner, child.Owner);
            Assert.AreEqual(WindowStartupLocation.CenterOwner, child.WindowStartupLocation);
            this.viewManager.Verify(x => x.GetView(ownerModel), Times.Once);
            this.config.Verify(x => x.GetActiveWindow(), Times.Never);
        }
        finally
        {
            child.Close();
            owner.Close();
        }
    }
}
