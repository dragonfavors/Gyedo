using NUnit.Framework;
using Gyedo;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gyedo.Wpf.Tests;

internal sealed class AsyncScreenProbe : Screen, IAsyncDisposable
{
    public Func<CancellationToken, Task> Initialize = _ => Task.CompletedTask;
    public Func<CancellationToken, Task> Activation = _ => Task.CompletedTask;
    public Func<Task> Deactivation = () => Task.CompletedTask;
    public Func<Task> Closure = () => Task.CompletedTask;
    public Func<Task> Disposal = () => Task.CompletedTask;
    public readonly List<string> Calls = new();
    public bool AllowClose = true;
    public int Initializations;
    public int Activations;
    public CancellationToken Token;
    protected override Task OnInitialActivatedAsync(CancellationToken token)
    {
        this.Calls.Add("initialize");
        this.Initializations++;
        this.Token = token;
        return this.Initialize(token);
    }
    protected override Task OnActivatedAsync(CancellationToken token)
    {
        this.Calls.Add("activate");
        this.Activations++;
        this.Token = token;
        return this.Activation(token);
    }
    protected override Task OnDeactivatedAsync() { this.Calls.Add("deactivate"); return this.Deactivation(); }
    protected override Task OnClosedAsync() { this.Calls.Add("close"); return this.Closure(); }
    public override Task<bool> CanCloseAsync() => Task.FromResult(this.AllowClose);
    public ValueTask DisposeAsync() { this.Calls.Add("dispose"); return new(this.Disposal()); }
}

[TestFixture]
public class ScreenActivationTests
{
    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Finish(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5));

    [Test]
    public async Task ActivationSetsStateBeforeHooksAndWaitsBeforeCompletionEvent()
    {
        var initialize = Gate();
        var activate = Gate();
        var screen = new AsyncScreenProbe { Initialize = _ => initialize.Task, Activation = _ => activate.Task };
        int events = 0;
        screen.Activated += (_, _) => events++;
        Task pending = screen.ActivateAsync();
        Assert.That(screen.IsActive, Is.True);
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Active));
        Assert.That(events, Is.Zero);
        Assert.That(pending.IsCompleted, Is.False);
        Assert.That(screen.Activations, Is.Zero);
        initialize.SetResult();
        activate.SetResult();
        await Finish(pending);
        Assert.That(screen.IsActive, Is.True);
        Assert.That(events, Is.EqualTo(1));
        Assert.That(screen.Calls, Is.EqualTo(new[] { "initialize", "activate" }));
    }

    [Test]
    public async Task DuplicateActivationRunsHooksOnce()
    {
        var gate = Gate();
        var screen = new AsyncScreenProbe { Activation = _ => gate.Task };
        Task first = screen.ActivateAsync();
        Task second = screen.ActivateAsync();
        gate.SetResult();
        await Finish(Task.WhenAll(first, second));
        Assert.That(screen.Activations, Is.EqualTo(1));
    }

    [Test]
    public async Task InitializationRepeatsOnlyAfterSuccessfulClose()
    {
        var screen = new AsyncScreenProbe();
        await screen.ActivateAsync();
        await screen.DeactivateAsync();
        await screen.ActivateAsync();
        Assert.That(screen.Initializations, Is.EqualTo(1));
        await screen.CloseAsync();
        await screen.ActivateAsync();
        Assert.That(screen.Initializations, Is.EqualTo(2));
    }

    [Test]
    public async Task DeactivationWaitsAndCancelsActiveTokenBeforeCleanup()
    {
        var gate = Gate();
        var screen = new AsyncScreenProbe { Deactivation = () => gate.Task };
        await screen.ActivateAsync();
        Task pending = screen.DeactivateAsync();
        Assert.That(screen.Token.IsCancellationRequested, Is.True);
        Assert.That(screen.IsActive, Is.False);
        Assert.That(pending.IsCompleted, Is.False);
        gate.SetResult();
        await Finish(pending);
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Deactivated));
    }

    [Test]
    public async Task ClosingSetsStateAndReleasesViewBeforeHookButWaitsForCompletionEvent()
    {
        var gate = Gate();
        var screen = new AsyncScreenProbe { Closure = () => gate.Task };
        screen.ViewReleaseRequested += (_, _) => screen.Calls.Add("release");
        screen.Closed += (_, _) => screen.Calls.Add("closed");
        await screen.ActivateAsync();
        Task close = screen.CloseAsync();
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Closed));
        Assert.That(screen.Calls, Does.Contain("release"));
        Assert.That(screen.Calls, Does.Not.Contain("closed"));
        Assert.That(close.IsCompleted, Is.False);
        gate.SetResult();
        await Finish(close);
        Assert.That(screen.Calls, Is.EqualTo(new[] { "initialize", "activate", "deactivate", "release", "close", "closed" }));
    }

    [Test]
    public async Task ClosingDuringActivationCancelsBeforeWaitingForItsFinally()
    {
        var cleaning = Gate();
        var cleaned = Gate();
        var screen = new AsyncScreenProbe
        {
            Activation = async token =>
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                finally { cleaning.SetResult(); await cleaned.Task; }
            },
        };
        Task activate = screen.ActivateAsync();
        Task close = screen.CloseAsync();
        await Finish(cleaning.Task);
        Assert.That(close.IsCompleted, Is.False);
        cleaned.SetResult();
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(activate));
        await Finish(close);
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Closed));
    }

    [Test]
    public async Task ExternalTokenCancelsAndRollsBackActivation()
    {
        using var cancellation = new CancellationTokenSource();
        var screen = new AsyncScreenProbe { Activation = token => Task.Delay(Timeout.Infinite, token) };
        Task pending = screen.ActivateAsync(cancellation.Token);
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(pending));
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Deactivated));
        Assert.That(screen.Calls, Does.Contain("deactivate"));
        screen.Activation = _ => Task.CompletedTask;
        await screen.ActivateAsync();
        Assert.That(screen.IsActive, Is.True);
    }

    [Test]
    public async Task CloseInvalidatesAnActivationAlreadyQueuedBehindDeactivation()
    {
        var gate = Gate();
        var screen = new AsyncScreenProbe { Deactivation = () => gate.Task };
        await screen.ActivateAsync();
        Task deactivate = screen.DeactivateAsync();
        Task activate = screen.ActivateAsync();
        Task close = screen.CloseAsync();
        gate.SetResult();
        await Finish(deactivate);
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(activate));
        await Finish(close);
        Assert.That(screen.Activations, Is.EqualTo(1));
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Closed));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ActivationFailurePropagatesAndCanBeRetried(bool initial)
    {
        var error = new InvalidOperationException("failed");
        var screen = new AsyncScreenProbe();
        if (initial) screen.Initialize = _ => Task.FromException(error);
        else screen.Activation = _ => Task.FromException(error);
        Exception actual = Assert.ThrowsAsync<InvalidOperationException>(async () => await screen.ActivateAsync());
        Assert.That(actual, Is.SameAs(error));
        Assert.That(screen.IsActive, Is.False);
        Assert.That(screen.Calls, Does.Contain("deactivate"));
        screen.Initialize = _ => Task.CompletedTask;
        screen.Activation = _ => Task.CompletedTask;
        await screen.ActivateAsync();
        Assert.That(screen.Initializations, Is.EqualTo(initial ? 2 : 1));
    }

    [Test]
    public async Task DeactivationFailureKeepsInactiveStateAndCanBeRetried()
    {
        var screen = new AsyncScreenProbe { Deactivation = () => Task.FromException(new InvalidOperationException()) };
        await screen.ActivateAsync();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await screen.CloseAsync());
        Assert.That(screen.IsActive, Is.False);
        Assert.That(screen.Calls, Does.Not.Contain("close"));
        screen.Deactivation = () => Task.CompletedTask;
        await screen.CloseAsync();
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Closed));
    }

    [Test]
    public async Task CloseFailureKeepsClosedStateAndRetriesCleanupWithoutReleasingViewAgain()
    {
        var screen = new AsyncScreenProbe { Closure = () => Task.FromException(new InvalidOperationException()) };
        int released = 0;
        screen.ViewReleaseRequested += (_, _) => released++;
        await screen.ActivateAsync();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await screen.CloseAsync());
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Closed));
        Assert.That(released, Is.EqualTo(1));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await screen.ActivateAsync());
        screen.Closure = () => Task.CompletedTask;
        await screen.CloseAsync();
        Assert.That(released, Is.EqualTo(1));
    }

    [Test]
    public void ReentrantTransitionFailsInsteadOfDeadlocking()
    {
        var screen = new AsyncScreenProbe();
        screen.Activation = _ => screen.CloseAsync();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Finish(screen.ActivateAsync()));
    }

    [Test]
    public async Task ChildCloseFinishesBeforeParentDetachAndAsyncDispose()
    {
        var closeGate = Gate();
        var disposeGate = Gate();
        var parent = new Conductor<Screen>();
        var child = new AsyncScreenProbe { Closure = () => closeGate.Task, Disposal = () => disposeGate.Task };
        await parent.ActivateItemAsync(child);
        await parent.ActivateAsync();
        Task<bool> closing = parent.CloseItemAsync(child);
        Assert.That(child.Parent, Is.SameAs(parent));
        Assert.That(child.Calls, Does.Not.Contain("dispose"));
        closeGate.SetResult();
        disposeGate.SetResult();
        Assert.That(await closing.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(child.Parent, Is.Null);
        Assert.That(parent.ActiveItem, Is.Null);
        Assert.That(child.Calls.IndexOf("close"), Is.LessThan(child.Calls.IndexOf("dispose")));
    }

    [Test]
    public async Task RejectedCloseDoesNotCancelActiveChild()
    {
        var parent = new Conductor<Screen>();
        var child = new AsyncScreenProbe { AllowClose = false };
        await parent.ActivateItemAsync(child);
        await parent.ActivateAsync();
        Assert.That(await child.RequestCloseAsync(), Is.False);
        Assert.That(child.Token.IsCancellationRequested, Is.False);
        Assert.That(child.IsActive, Is.True);
    }

    [Test]
    public async Task ParentCloseCancelsPendingChildActivationAndWaitsForChildClosure()
    {
        var parent = new Conductor<Screen>.Collection.AllActive();
        var child = new AsyncScreenProbe { Activation = token => Task.Delay(Timeout.Infinite, token) };
        await parent.ActivateItemAsync(child);
        Task activate = parent.ActivateAsync();
        Task close = parent.CloseAsync();
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(activate));
        await Finish(close);
        Assert.That(parent.ScreenState, Is.EqualTo(ScreenState.Closed));
        Assert.That(child.ScreenState, Is.EqualTo(ScreenState.Closed));
        Assert.That(child.Calls, Does.Contain("dispose"));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public async Task ChildCanRequestCloseWhileItsNavigationIsStillLoading(int kind)
    {
        ConductorBase<Screen> parent = kind switch
        {
            0 => new Conductor<Screen>(),
            1 => new Conductor<Screen>.Collection.OneActive(),
            2 => new Conductor<Screen>.Collection.AllActive(),
            _ => new Conductor<Screen>.StackNavigation(),
        };
        await parent.ActivateAsync();
        var child = new AsyncScreenProbe { Activation = token => Task.Delay(Timeout.Infinite, token) };
        Task navigate = parent.ActivateItemAsync(child);
        Task<bool> close = child.RequestCloseAsync();
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(navigate));
        Assert.That(await close.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(child.ScreenState, Is.EqualTo(ScreenState.Closed));
        Assert.That(child.Parent, Is.Null);
        Assert.That(parent.GetChildren(), Is.Empty);
    }

    [Test]
    public async Task ClearCancelsPendingNavigationBeforeWaitingForTheQueue()
    {
        var parent = new Conductor<Screen>.Collection.OneActive();
        await parent.ActivateAsync();
        var child = new AsyncScreenProbe { Activation = token => Task.Delay(Timeout.Infinite, token) };
        Task navigate = parent.ActivateItemAsync(child);
        Task<bool> clear = parent.ClearAsync();
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(navigate));
        Assert.That(await clear.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(parent.Items, Is.Empty);
    }

    [Test]
    public async Task CapturedExecutionContextDoesNotRejectWorkAfterHookCompletes()
    {
        var start = Gate();
        var screen = new AsyncScreenProbe();
        Task later = null;
        screen.Activation = _ =>
        {
            later = Task.Run(async () => { await start.Task; await screen.DeactivateAsync(); });
            return Task.CompletedTask;
        };
        await screen.ActivateAsync();
        start.SetResult();
        await Finish(later);
        Assert.That(screen.IsActive, Is.False);
    }

    [Test]
    public async Task ForeignOwnedItemDoesNotCauseCurrentItemToBeClosed()
    {
        var first = new Conductor<Screen>();
        var second = new Conductor<Screen>();
        var existing = new AsyncScreenProbe();
        var foreign = new AsyncScreenProbe();
        await first.ActivateItemAsync(existing);
        await first.ActivateAsync();
        await second.ActivateItemAsync(foreign);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await first.ActivateItemAsync(foreign));
        Assert.That(first.ActiveItem, Is.SameAs(existing));
        Assert.That(existing.IsActive, Is.True);
        Assert.That(existing.Calls, Does.Not.Contain("dispose"));
    }
}
