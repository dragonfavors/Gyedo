using NUnit.Framework;
using Stylet;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace StyletUnitTests;

[TestFixture]
public class ApplicationDispatcherTests
{
    private DispatcherHost host;
    [SetUp]
    public void SetUp() => this.host = new DispatcherHost();
    [TearDown]
    public void TearDown() => this.host.Dispose();
    private static Task Finish(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5));

    [TestCase(DispatchPriority.Lowest, DispatcherPriority.Background)]
    [TestCase(DispatchPriority.Low, DispatcherPriority.Input)]
    [TestCase(DispatchPriority.Middle, DispatcherPriority.Loaded)]
    [TestCase(DispatchPriority.High, DispatcherPriority.Render)]
    [TestCase(DispatchPriority.Highest, DispatcherPriority.Normal)]
    public async Task MapsEachPriorityToWpf(DispatchPriority priority, DispatcherPriority expected)
    {
        DispatcherPriority? posted = null;
        this.host.Native.Hooks.OperationPosted += (_, e) => posted = e.Operation.Priority;
        await Finish(this.host.Adapter.InvokeAsync(() => { }, priority));
        Assert.That(posted, Is.EqualTo(expected));
    }

    [Test]
    public async Task DefaultAndPostUseBackgroundPriority()
    {
        var priorities = new List<DispatcherPriority>();
        this.host.Native.Hooks.OperationPosted += (_, e) => priorities.Add(e.Operation.Priority);
        var posted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.host.Adapter.Post(() => posted.SetResult());
        await Finish(posted.Task);
        await Finish(this.host.Adapter.InvokeAsync(() => { }));
        Assert.That(priorities, Is.EqualTo(new[] { DispatcherPriority.Background, DispatcherPriority.Background }));
    }

    [Test]
    public async Task QueuedWorkRunsInPriorityOrderAndKeepsOrderWithinALevel()
    {
        var calls = new List<int>();
        var tasks = new List<Task>();
        using (this.host.Pause())
        {
            for (int level = 0; level < 5; level++)
            {
                int value = level;
                tasks.Add(this.host.Adapter.InvokeAsync(() => { calls.Add(value * 10); }, (DispatchPriority)level));
                tasks.Add(this.host.Adapter.InvokeAsync(() => { calls.Add(value * 10 + 1); }, (DispatchPriority)level));
            }
        }
        await Finish(Task.WhenAll(tasks));
        Assert.That(calls, Is.EqualTo(new[] { 40, 41, 30, 31, 20, 21, 10, 11, 0, 1 }));
    }

    [Test]
    public async Task InvokeAsyncAlwaysQueuesEvenOnTheTargetThread()
    {
        bool ran = false;
        bool ranBeforeReturning = true;
        Task queued = null;
        await Finish(this.host.Adapter.InvokeAsync(() =>
        {
            queued = this.host.Adapter.InvokeAsync(() => { ran = true; });
            ranBeforeReturning = ran;
        }));
        await Finish(queued);
        Assert.That(ranBeforeReturning, Is.False);
        Assert.That(ran, Is.True);
    }

    [Test]
    public async Task SyncSendOnTargetThreadDoesNotPumpOtherQueuedWork()
    {
        var order = new List<string>();
        Task queued = null;
        await Finish(this.host.Adapter.InvokeAsync(() =>
        {
            queued = this.host.Adapter.InvokeAsync(() => { order.Add("queued"); }, DispatchPriority.Highest);
            this.host.Adapter.Send(() => order.Add("send"));
            order.Add("returned");
        }));
        await Finish(queued);
        Assert.That(order, Is.EqualTo(new[] { "send", "returned", "queued" }));
    }

    [Test]
    public void SyncSendFromAnotherThreadWaitsForCompletion()
    {
        int thread = 0;
        this.host.Adapter.Send(() => thread = Environment.CurrentManagedThreadId, DispatchPriority.Low);
        Assert.That(thread, Is.EqualTo(this.host.ThreadId));
    }

    [Test]
    public async Task AsyncCallbackWaitsForCompletionAndResumesOnUi()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int before = 0, after = 0;
        Task operation = this.host.Adapter.InvokeAsync(async () =>
        {
            before = Environment.CurrentManagedThreadId;
            entered.SetResult();
            await release.Task;
            after = Environment.CurrentManagedThreadId;
        });
        await Finish(entered.Task);
        Assert.That(operation.IsCompleted, Is.False);
        release.SetResult();
        await Finish(operation);
        Assert.That(before, Is.EqualTo(this.host.ThreadId));
        Assert.That(after, Is.EqualTo(this.host.ThreadId));
    }

    [Test]
    public async Task ResultOverloadsUnwrapAsyncResults()
    {
        Task<int> value = this.host.Adapter.InvokeAsync(() => 12);
        Assert.That(await value.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(12));
        Task<int> asynchronous = this.host.Adapter.InvokeAsync(async () => { await Task.Yield(); return 34; });
        Assert.That(await asynchronous.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(34));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AsyncCallbackErrorsBeforeAndAfterAwaitReachCaller(bool afterAwait)
    {
        var error = new InvalidOperationException("callback failure");
        Func<Task> callback = afterAwait
            ? async () => { await Task.Yield(); throw error; }
            : () => throw error;
        Task operation = this.host.Adapter.InvokeAsync(callback);
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () => await Finish(operation)), Is.SameAs(error));
        Assert.That(operation.IsFaulted, Is.True);
    }

    [Test]
    public void ActionAndResultErrorsReachCallerThroughTask()
    {
        var error = new InvalidOperationException("synchronous failure");
        Task action = this.host.Adapter.InvokeAsync((Action)(() => throw error));
        Task<int> value = this.host.Adapter.InvokeAsync((Func<int>)(() => throw error));
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () => await Finish(action)), Is.SameAs(error));
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () => await Finish(value)), Is.SameAs(error));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CancellationBeforeExecutionPreventsTheCallback(bool alreadyCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        bool ran = false;
        Task operation;
        using (this.host.Pause())
        {
            if (alreadyCancelled) cancellation.Cancel();
            operation = this.host.Adapter.InvokeAsync(() => { ran = true; }, cancellationToken: cancellation.Token);
            if (!alreadyCancelled) cancellation.Cancel();
        }
        try { await Finish(operation); }
        catch (OperationCanceledException) { }
        Assert.That(operation.IsCanceled, Is.True);
        Assert.That(ran, Is.False);
    }

    [Test]
    public async Task RunningCallbackIsNotCompletedEarlyBySchedulingCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> operation = this.host.Adapter.InvokeAsync(() =>
        {
            entered.SetResult();
            return release.Task;
        }, cancellationToken: cancellation.Token);
        await Finish(entered.Task);
        cancellation.Cancel();
        Assert.That(operation.IsCompleted, Is.False);
        release.SetResult(42);
        Assert.That(await operation.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(42));
    }

    [Test]
    public async Task CooperativeCancellationOfRunningCallbackIsPreserved()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task operation = this.host.Adapter.InvokeAsync(async () =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, cancellation.Token);
        }, cancellationToken: cancellation.Token);
        await Finish(entered.Task);
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(operation));
        Assert.That(operation.IsCanceled, Is.True);
    }

    [Test]
    public void ShutdownCancelsQueuedWorkInsteadOfLeavingTaskPending()
    {
        bool ran = false;
        Task operation;
        using (this.host.Pause())
        {
            operation = this.host.Adapter.InvokeAsync(() => { ran = true; });
            this.host.Native.BeginInvokeShutdown(DispatcherPriority.Send);
        }
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(operation));
        Assert.That(operation.IsCanceled, Is.True);
        Assert.That(ran, Is.False);
    }

    [Test]
    public void PostingAfterShutdownReturnsCancelledTask()
    {
        this.host.Dispose();
        Task action = this.host.Adapter.InvokeAsync(() => { });
        Task<int> result = this.host.Adapter.InvokeAsync(() => Task.FromResult(1));
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(action));
        Assert.CatchAsync<OperationCanceledException>(async () => await Finish(result));
        Assert.That(action.IsCanceled && result.IsCanceled, Is.True);
    }

    [Test]
    public async Task CompletionOfStartedTaskDoesNotNeedAnAdditionalUiDispatch()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> operation = this.host.Adapter.InvokeAsync(() => { entered.SetResult(); return release.Task; });
        await Finish(entered.Task);
        this.host.Dispose();
        release.SetResult(99);
        Assert.That(await operation.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(99));
    }

    [Test]
    public void InvalidArgumentsAndNullTasksAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => this.host.Adapter.InvokeAsync((Func<Task>)null));
        Assert.Throws<ArgumentOutOfRangeException>(() => this.host.Adapter.InvokeAsync(() => { }, (DispatchPriority)5));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Finish(this.host.Adapter.InvokeAsync((Func<Task>)(() => null))));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Finish(this.host.Adapter.InvokeAsync((Func<Task<int>>)(() => null))));
    }

    private sealed class DispatcherHost : IDisposable
    {
        private readonly Thread thread;
        private bool disposed;
        public Dispatcher Native { get; }
        public ApplicationDispatcher Adapter { get; }
        public int ThreadId => this.thread.ManagedThreadId;

        public DispatcherHost()
        {
            var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.thread = new Thread(() =>
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                ready.SetResult(dispatcher);
                Dispatcher.Run();
            }) { IsBackground = true, Name = "Dispatcher test" };
            this.thread.SetApartmentState(ApartmentState.STA);
            this.thread.Start();
            this.Native = ready.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            this.Adapter = new ApplicationDispatcher(this.Native);
        }

        public IDisposable Pause()
        {
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            Task operation = this.Native.InvokeAsync(() =>
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Dispatcher test pause timed out");
            }, DispatcherPriority.Send).Task;
            if (!entered.Wait(TimeSpan.FromSeconds(5)))
            {
                release.Set();
                throw new TimeoutException("Dispatcher test did not start");
            }
            return new PauseScope(entered, release, operation);
        }

        public void Dispose()
        {
            if (this.disposed) return;
            this.disposed = true;
            if (!this.Native.HasShutdownStarted) this.Native.BeginInvokeShutdown(DispatcherPriority.Send);
            if (Thread.CurrentThread != this.thread && !this.thread.Join(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Dispatcher test thread did not stop");
        }

        private sealed class PauseScope(ManualResetEventSlim entered, ManualResetEventSlim release, Task operation) : IDisposable
        {
            public void Dispose()
            {
                release.Set();
                operation.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                entered.Dispose();
                release.Dispose();
            }
        }
    }
}
