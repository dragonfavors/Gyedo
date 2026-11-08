using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NUnit.Framework;
using Stylet;
using Stylet.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace StyletUnitTests;

// 실제 View 탐색·바인딩·기본 스타일·WindowManager를 함께 실행합니다. 창은 화면 밖에 표시합니다.
[TestFixture, Category("WpfIntegration"), Apartment(ApartmentState.STA)]
public class WpfUiFlowTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(8);
    private SynchronizationContext previousContext;
    private IDispatcher previousDispatcher;
    private ViewManager views;
    private WindowManager windows;
    private readonly List<Window> createdWindows = new();
    private readonly List<Screen> models = new();
    private readonly List<Action> releaseGates = new();
    private readonly Dictionary<Window, TaskCompletionSource> loaded = new();
    private readonly Dictionary<Window, TaskCompletionSource> closed = new();

    [SetUp]
    public void SetUp()
    {
        this.previousContext = SynchronizationContext.Current;
        this.previousDispatcher = Execute.Dispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        Execute.Dispatcher = new ApplicationDispatcher(Dispatcher.CurrentDispatcher);
        this.views = new ViewManager(new ViewManagerConfig
        {
            ViewAssemblies = new List<System.Reflection.Assembly> { typeof(WpfUiFlowTests).Assembly },
            ViewFactory = this.CreateView,
        });
        this.windows = new WindowManager(this.views, () => this.Track(new MessageBoxViewModel()),
            new WindowConfig(() => this.createdWindows.LastOrDefault(x => x.IsVisible)));
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await this.CleanupAsync(); }
        finally
        {
            Execute.Dispatcher = this.previousDispatcher;
            SynchronizationContext.SetSynchronizationContext(this.previousContext);
            this.models.Clear();
            this.createdWindows.Clear();
            this.releaseGates.Clear();
            this.loaded.Clear();
            this.closed.Clear();
        }
    }

    private UIElement CreateView(Type type)
    {
        var view = (FrameworkElement)Activator.CreateInstance(type);
        view.Resources[View.ViewManagerResourceKey] = this.views;
        if (view is Window window)
        {
            window.Left = -32000;
            window.Top = -32000;
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            this.createdWindows.Add(window);
            this.loaded[window] = NewSignal();
            this.closed[window] = NewSignal();
            window.Loaded += (_, _) => this.loaded[window].TrySetResult();
            window.Closed += (_, _) => this.closed[window].TrySetResult();
        }
        return view;
    }

    private T Track<T>(T model) where T : Screen { this.models.Add(model); return model; }
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource NewGate()
    {
        TaskCompletionSource gate = NewSignal();
        this.releaseGates.Add(() => gate.TrySetResult());
        return gate;
    }
    private static Task Wait(Task task) => task.WaitAsync(Limit);
    private static Task FlushUiAsync() => Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task.WaitAsync(Limit);
    private static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();

    // 모달 창을 표시한 테스트가 실패해도 테스트 Task 완료 전에 모달 루프를 끝냅니다.
    private async Task RunFlowAsync(Func<Task> flow)
    {
        try { await flow(); }
        finally { await this.CleanupAsync(); }
    }

    private async Task CleanupAsync()
    {
        foreach (Action release in this.releaseGates) release();
        foreach (FlowScreen screen in this.models.OfType<FlowScreen>())
        {
            screen.Guard = () => Task.FromResult(true);
            screen.Closing = () => Task.CompletedTask;
        }
        try
        {
            if (this.windows != null) await Wait(this.windows.CloseAllAsync());
        }
        finally
        {
            foreach (Window window in this.createdWindows.Where(x => !this.closed[x].Task.IsCompleted).ToArray())
            {
                window.Closing += (_, e) => e.Cancel = false;
                window.Close();
            }
            foreach (Screen model in this.models)
            {
                model.AlwaysMessenger.UnregisterAll(model);
                await Wait(model.CloseAsync());
                if (model is FlowScreen screen && screen.DisposeCount == 0)
                    await screen.DisposeAsync();
            }
        }
    }

    [Test]
    public Task WindowBindingAndCloseButtonWaitForCleanup() => this.RunFlowAsync(async () =>
    {
        var model = this.Track(new FlowWindowViewModel { DisplayName = "UI integration" });
        await Wait(this.windows.ShowWindowAsync(model));
        var view = (FlowWindowView)this.views.GetView(model);
        await Wait(model.ViewLoaded.Task);
        Assert.That(view.IsVisible, Is.True);
        Assert.That(view.DataContext, Is.SameAs(model));
        Assert.That(view.Title, Is.EqualTo(model.DisplayName));
        model.Text = "bound text";
        await FlushUiAsync();
        Assert.That(view.Text.Text, Is.EqualTo("bound text"));

        TaskCompletionSource cleanup = this.NewGate();
        model.Closing = () => cleanup.Task;
        Click(view.CloseButton);
        await Wait(model.ClosingEntered.Task);
        Assert.That(model.CloseCommand.IsRunning, Is.True);
        Assert.That(model.ScreenState, Is.EqualTo(ScreenState.Closed));
        Assert.That(view.IsVisible, Is.True);
        Assert.That(this.views.GetView(model), Is.Null);
        bool uiResponded = false;
        await Execute.InvokeAsync(() => { uiResponded = true; });
        Assert.That(uiResponded, Is.True);
        cleanup.SetResult();
        await Wait(model.CloseCommand.ExecutionTask);
        await Wait(this.closed[view].Task);
        Assert.That(view.IsVisible, Is.False);
        Assert.That(model.Parent, Is.Null);
        Assert.That(model.CloseCount, Is.EqualTo(1));
        Assert.That(model.DisposeCount, Is.Zero, "루트 VM 폐기는 WindowManager의 책임이 아닙니다.");
    });

    [Test]
    public Task NativeWindowCloseCanBeRejectedThenRetried() => this.RunFlowAsync(async () =>
    {
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.releaseGates.Add(() => decision.TrySetResult(true));
        var model = this.Track(new FlowWindowViewModel { Guard = () => decision.Task });
        await Wait(this.windows.ShowWindowAsync(model));
        var view = (Window)this.views.GetView(model);
        await Wait(this.loaded[view].Task);
        view.Close();
        Task<bool> pending = model.RequestCloseAsync();
        Assert.That(view.IsVisible, Is.True);
        Assert.That(pending.IsCompleted, Is.False);
        decision.SetResult(false);
        Assert.That(await pending.WaitAsync(Limit), Is.False);
        Assert.That(model.IsActive, Is.True);
        Assert.That(view.IsVisible, Is.True);
        model.Guard = () => Task.FromResult(true);
        view.Close();
        Assert.That(await model.RequestCloseAsync().WaitAsync(Limit), Is.True);
        await Wait(this.closed[view].Task);
        Assert.That(model.GuardCount, Is.EqualTo(2));
        Assert.That(model.CloseCount, Is.EqualTo(1));
    });

    [TestCase(true)]
    [TestCase(false)]
    public Task ModalDialogReturnsButtonResultAfterCleanup(bool accept) => this.RunFlowAsync(async () =>
    {
        var owner = this.Track(new FlowWindowViewModel());
        await Wait(this.windows.ShowWindowAsync(owner));
        var ownerView = (Window)this.views.GetView(owner);
        var model = this.Track(new FlowDialogViewModel());
        TaskCompletionSource cleanup = this.NewGate();
        model.Closing = () => cleanup.Task;
        Task<bool?> dialog = this.windows.ShowDialogAsync(model, owner);
        var view = (FlowDialogView)this.views.GetView(model);
        await Wait(this.loaded[view].Task);
        Assert.That(view.Owner, Is.SameAs(ownerView));
        Assert.That(view.IsVisible, Is.True);
        Assert.That(dialog.IsCompleted, Is.False);
        Click(accept ? view.AcceptButton : view.CancelButton);
        await Wait(model.ClosingEntered.Task);
        Assert.That(dialog.IsCompleted, Is.False);
        Assert.That(view.IsVisible, Is.True);
        cleanup.SetResult();
        Assert.That(await dialog.WaitAsync(Limit), Is.EqualTo(accept));
        await Wait(accept ? model.AcceptCommand.ExecutionTask : model.CancelCommand.ExecutionTask);
        Assert.That(view.IsVisible, Is.False);
        Assert.That(ownerView.IsVisible, Is.True);
        Assert.That(model.Parent, Is.Null);
    });

    [TestCase(MessageBoxResult.Yes)]
    [TestCase(MessageBoxResult.Cancel)]
    public Task BuiltInMessageBoxRoutesActualButtonClick(MessageBoxResult choice) => this.RunFlowAsync(async () =>
    {
        var owner = this.Track(new FlowWindowViewModel());
        await Wait(this.windows.ShowWindowAsync(owner));
        Task<MessageBoxResult> result = this.windows.ShowMessageBoxAsync("UI test", buttons: MessageBoxButton.YesNoCancel);
        var model = this.models.OfType<MessageBoxViewModel>().Single();
        var view = (MessageBoxView)this.views.GetView(model);
        await Wait(this.loaded[view].Task);
        await FlushUiAsync();
        Button button = Descendants(view).OfType<Button>().Single(x => Equals(x.Tag, choice));
        Click(button);
        Assert.That(await result.WaitAsync(Limit), Is.EqualTo(choice));
        Assert.That(view.IsVisible, Is.False);
        Assert.That(model.ScreenState, Is.EqualTo(ScreenState.Closed));
        Assert.That(this.views.GetView(model), Is.Null);
    });

    [TestCase(false)]
    [TestCase(true)]
    public Task RealTabStyleSwitchesViewsAndPreservesMessengerLifetimes(bool fail) => this.RunFlowAsync(async () =>
    {
        var first = this.Track(new FlowPageViewModel { DisplayName = "first" });
        var second = this.Track(new FlowPageViewModel { DisplayName = "second" });
        var model = this.Track(new FlowTabsViewModel(first, second));
        await Wait(this.windows.ShowWindowAsync(model));
        var view = (FlowTabsView)this.views.GetView(model);
        await Wait(first.ViewLoaded.Task);
        var firstView = (FlowPageView)this.views.GetView(first);
        Assert.That(view.Tabs.SelectedItem, Is.SameAs(first));
        AssertDelivery(first, active: true, longer: true);
        AssertDelivery(second, active: false, longer: false);

        TaskCompletionSource loading = this.NewGate();
        second.Activation = token => loading.Task.WaitAsync(token);
        view.Tabs.SetCurrentValue(Selector.SelectedItemProperty, second);
        await Wait(second.ActivationEntered.Task);
        Assert.That(view.Tabs.IsEnabled, Is.False);
        Assert.That(model.ActiveItem, Is.SameAs(first));
        if (fail) loading.SetException(new InvalidOperationException("load failed"));
        else loading.SetResult();
        await FlushUiAsync();
        Assert.That(view.Tabs.IsEnabled, Is.True);
        Assert.That(model.ActiveItem, Is.SameAs(fail ? first : second));
        Assert.That(view.Tabs.SelectedItem, Is.SameAs(model.ActiveItem));
        AssertDelivery(first, active: fail, longer: true);
        AssertDelivery(second, active: !fail, longer: true);
        if (!fail)
        {
            await Wait(second.ViewLoaded.Task);
            await FlushUiAsync();
            Assert.That(((FlowPageView)this.views.GetView(second)).Text.Text, Is.EqualTo("always"));
            view.Tabs.SetCurrentValue(Selector.SelectedItemProperty, first);
            await FlushUiAsync();
        }
        Assert.That(this.views.GetView(first), Is.SameAs(firstView));
        Assert.That(first.InitialCount, Is.EqualTo(1));
        Assert.That(await model.CloseItemAsync(second), Is.True);
        Assert.That(second.DisposeCount, Is.EqualTo(1));
        Assert.That(this.views.GetView(second), Is.Null);
        AssertDelivery(second, active: false, longer: false);
        await model.RequestCloseAsync();
        Assert.That(view.IsVisible, Is.False);
        Assert.That(first.DisposeCount, Is.EqualTo(1));
    });

    [Test]
    public Task ClosingDuringInitialLoadWaitsForCancellationCleanup() => this.RunFlowAsync(async () =>
    {
        TaskCompletionSource cleaning = NewSignal();
        TaskCompletionSource cleanup = this.NewGate();
        var model = this.Track(new FlowWindowViewModel
        {
            Activation = async token =>
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                finally { cleaning.SetResult(); await cleanup.Task; }
            },
        });
        Task opening = this.windows.ShowWindowAsync(model);
        await Wait(model.ActivationEntered.Task);
        var view = (Window)this.views.GetView(model);
        Task<bool> closing = model.RequestCloseAsync();
        await Wait(cleaning.Task);
        Assert.That(view.IsVisible, Is.False, "초기 활성화가 끝나기 전에는 표시하지 않습니다.");
        Assert.That(opening.IsCompleted || closing.IsCompleted, Is.False);
        cleanup.SetResult();
        try
        {
            await Wait(opening);
            Assert.Fail("활성화는 취소되어야 합니다.");
        }
        catch (OperationCanceledException) { }
        Assert.That(await closing.WaitAsync(Limit), Is.True);
        Assert.That(model.CloseCount, Is.EqualTo(1));
        Assert.That(model.Parent, Is.Null);
        Assert.That(this.views.GetView(model), Is.Null);
        Assert.That(this.closed[view].Task.IsCompleted, Is.True);
    });

    [Test]
    public Task PrioritizedUiUpdatesReachTheBoundViewInQueueOrder() => this.RunFlowAsync(async () =>
    {
        var model = this.Track(new FlowWindowViewModel());
        await Wait(this.windows.ShowWindowAsync(model));
        var view = (FlowWindowView)this.views.GetView(model);
        await Wait(this.loaded[view].Task);
        int uiThread = Environment.CurrentManagedThreadId;
        await Execute.RunAsync(() => { model.Text = "immediate"; });
        var order = new List<DispatchPriority>();
        Task[] updates = Enum.GetValues<DispatchPriority>().Select(priority => Execute.InvokeAsync(() =>
        {
            Assert.That(Environment.CurrentManagedThreadId, Is.EqualTo(uiThread));
            order.Add(priority);
            model.Text = priority.ToString();
        }, priority)).ToArray();
        Assert.That(model.Text, Is.EqualTo("immediate"), "InvokeAsync는 UI에서도 게시해야 합니다.");
        await Wait(Task.WhenAll(updates));
        await FlushUiAsync();
        Assert.That(order, Is.EqualTo(Enum.GetValues<DispatchPriority>().Reverse()));
        Assert.That(view.Text.Text, Is.EqualTo(nameof(DispatchPriority.Lowest)));
    });

    private static void AssertDelivery(FlowScreen model, bool active, bool longer)
    {
        model.Received.Clear();
        model.PublishAll();
        Assert.That(model.Received.GetValueOrDefault("active"), Is.EqualTo(active ? 1 : 0));
        Assert.That(model.Received.GetValueOrDefault("long"), Is.EqualTo(longer ? 1 : 0));
        Assert.That(model.Received.GetValueOrDefault("always"), Is.EqualTo(1));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject nested in Descendants(child)) yield return nested;
        }
    }

    private sealed class WindowConfig(Func<Window> active) : IWindowManagerConfig
    {
        public Window GetActiveWindow() => active();
    }

    public sealed record FlowMessage(string Source);

    public class FlowScreen : Screen, IRecipient<FlowMessage>, IAsyncDisposable
    {
        public Func<CancellationToken, Task> Activation = _ => Task.CompletedTask;
        public Func<Task> Closing = () => Task.CompletedTask;
        public Func<Task<bool>> Guard = () => Task.FromResult(true);
        public readonly TaskCompletionSource ActivationEntered = NewSignal();
        public readonly TaskCompletionSource ClosingEntered = NewSignal();
        public readonly TaskCompletionSource ViewLoaded = NewSignal();
        public readonly Dictionary<string, int> Received = new();
        public int InitialCount, GuardCount, CloseCount, DisposeCount;
        private string text = "initial";
        public string Text { get => this.text; set => this.SetProperty(ref this.text, value); }
        protected override Task OnInitialActivatedAsync(CancellationToken token) { this.InitialCount++; return Task.CompletedTask; }
        protected override Task OnActivatedAsync(CancellationToken token) { this.ActivationEntered.TrySetResult(); return this.Activation(token); }
        protected override Task OnClosedAsync() { this.CloseCount++; this.ClosingEntered.TrySetResult(); return this.Closing(); }
        protected override Task OnViewLoadedAsync() { this.ViewLoaded.TrySetResult(); return Task.CompletedTask; }
        public override Task<bool> CanCloseAsync() { this.GuardCount++; return this.Guard(); }
        public ValueTask DisposeAsync() { this.DisposeCount++; return ValueTask.CompletedTask; }
        public void Receive(FlowMessage message)
        {
            this.Received[message.Source] = this.Received.GetValueOrDefault(message.Source) + 1;
            this.Text = message.Source;
        }
        public void PublishAll()
        {
            this.Messenger.Send(new FlowMessage("active"));
            this.LongMessenger.Send(new FlowMessage("long"));
            this.AlwaysMessenger.Send(new FlowMessage("always"));
        }
    }

    public sealed class FlowWindowViewModel : FlowScreen
    {
        public IAsyncRelayCommand CloseCommand { get; }
        public FlowWindowViewModel() => this.CloseCommand = new AsyncRelayCommand(async () => { await this.RequestCloseAsync(); });
    }

    public sealed class FlowDialogViewModel : FlowScreen
    {
        public IAsyncRelayCommand AcceptCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }
        public FlowDialogViewModel()
        {
            this.AcceptCommand = new AsyncRelayCommand(async () => { await this.RequestCloseAsync(true); });
            this.CancelCommand = new AsyncRelayCommand(async () => { await this.RequestCloseAsync(false); });
        }
    }

    public sealed class FlowPageViewModel : FlowScreen { }

    public sealed class FlowTabsViewModel(FlowPageViewModel first, FlowPageViewModel second) : Conductor<Screen>.Collection.OneActive
    {
        protected override async Task OnInitialActivatedAsync(CancellationToken token)
        {
            await this.AddItemAsync(first);
            await this.AddItemAsync(second);
            await this.ActivateItemAsync(first);
        }
    }

    public class FlowWindowView : Window
    {
        public TextBlock Text { get; } = new();
        public Button CloseButton { get; } = new() { Content = "Close" };
        public FlowWindowView()
        {
            this.Width = 320;
            this.Height = 180;
            this.Text.SetBinding(TextBlock.TextProperty, new Binding("Text"));
            this.CloseButton.SetBinding(ButtonBase.CommandProperty, new Binding("CloseCommand"));
            var panel = new StackPanel();
            panel.Children.Add(this.Text);
            panel.Children.Add(this.CloseButton);
            this.Content = panel;
        }
    }

    public sealed class FlowDialogView : Window
    {
        public Button AcceptButton { get; } = new() { Content = "Accept" };
        public Button CancelButton { get; } = new() { Content = "Cancel" };
        public FlowDialogView()
        {
            this.Width = 240;
            this.Height = 140;
            this.AcceptButton.SetBinding(ButtonBase.CommandProperty, new Binding("AcceptCommand"));
            this.CancelButton.SetBinding(ButtonBase.CommandProperty, new Binding("CancelCommand"));
            var panel = new StackPanel();
            panel.Children.Add(this.AcceptButton);
            panel.Children.Add(this.CancelButton);
            this.Content = panel;
        }
    }

    public sealed class FlowTabsView : Window
    {
        public TabControl Tabs { get; } = new();
        public FlowTabsView()
        {
            this.Width = 360;
            this.Height = 220;
            this.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Stylet;component/Xaml/StyletResourceDictionary.xaml"),
            });
            this.Tabs.Style = (Style)this.FindResource("StyletConductorTabControl");
            this.Content = this.Tabs;
        }
    }

    public sealed class FlowPageView : UserControl
    {
        public TextBlock Text { get; } = new();
        public FlowPageView()
        {
            this.Text.SetBinding(TextBlock.TextProperty, new Binding("Text"));
            this.Content = this.Text;
        }
    }
}
