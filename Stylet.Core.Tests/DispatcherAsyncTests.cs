using Moq;
using NUnit.Framework;
using Stylet;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StyletUnitTests;

[TestFixture, NonParallelizable]
public class DispatcherAsyncTests
{
    private IDispatcher original;
    [SetUp]
    public void SetUp()
    {
        this.original = Execute.Dispatcher;
        Execute.Dispatcher = SynchronousDispatcher.Instance;
    }
    [TearDown]
    public void TearDown() => Execute.Dispatcher = this.original;

    [TestCase(false)]
    [TestCase(true)]
    public async Task HelpersAwaitTheInnerTask(bool post)
    {
        var finish = new TaskCompletionSource();
        bool completed = false;
        Func<Task> callback = async () => { await finish.Task; completed = true; };
        Task task = post ? Execute.InvokeAsync(callback) : Execute.RunAsync(callback);
        Assert.That(task.IsCompleted, Is.False);
        finish.SetResult();
        await task;
        Assert.That(completed, Is.True);
    }

    [Test]
    public async Task ResultOverloadsReturnValuesWithoutNestedTasks()
    {
        Assert.That(await Execute.RunAsync(() => 12), Is.EqualTo(12));
        Task<int> asynchronous = Execute.RunAsync(() => Task.FromResult(34));
        Assert.That(await asynchronous, Is.EqualTo(34));
        Assert.That(await Execute.InvokeAsync(() => 56), Is.EqualTo(56));
        Task<int> posted = Execute.InvokeAsync(() => Task.FromResult(78));
        Assert.That(await posted, Is.EqualTo(78));
    }

    [Test]
    public void SynchronousCallbackExceptionsAreReturnedThroughTask()
    {
        var error = new InvalidOperationException("callback");
        Task task = null;
        Assert.DoesNotThrow(() => task = Execute.RunAsync((Action)(() => throw error)));
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () => await task), Is.SameAs(error));
    }

    [Test]
    public void AlreadyCancelledCallbacksDoNotRun()
    {
        var token = new CancellationToken(true);
        bool ran = false;
        Task task = Execute.RunAsync(() => { ran = true; }, cancellationToken: token);
        Assert.That(task.IsCanceled, Is.True);
        Assert.That(ran, Is.False);
        Assert.CatchAsync<OperationCanceledException>(async () => await task);
    }

    [Test]
    public async Task CooperativeCancellationIsPreserved()
    {
        using var cancellation = new CancellationTokenSource();
        Task task = Execute.RunAsync(() => Task.Delay(Timeout.Infinite, cancellation.Token), cancellationToken: cancellation.Token);
        cancellation.Cancel();
        try { await task; }
        catch (OperationCanceledException) { }
        Assert.That(task.IsCanceled, Is.True);
    }

    [Test]
    public void NullAndInvalidPriorityAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Execute.RunAsync((Action)null));
        Assert.Throws<ArgumentOutOfRangeException>(() => Execute.RunAsync(() => { }, (DispatchPriority)5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Execute.Run(() => { }, (DispatchPriority)(-1)));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Execute.RunAsync((Func<Task>)(() => null)));
    }

    [Test]
    public async Task PostingForwardsPriorityAndCancellationEvenWhenAlreadyOnUiThread()
    {
        using var cancellation = new CancellationTokenSource();
        var dispatcher = new Mock<IDispatcher>(MockBehavior.Strict);
        Func<Task<int>> callback = () => Task.FromResult(42);
        dispatcher.Setup(x => x.InvokeAsync(callback, DispatchPriority.High, cancellation.Token)).ReturnsAsync(42);
        Execute.Dispatcher = dispatcher.Object;
        Assert.That(await Execute.InvokeAsync(callback, DispatchPriority.High, cancellation.Token), Is.EqualTo(42));
        dispatcher.VerifyAll();
    }

    [Test]
    public async Task NonUiInvocationForwardsTheAsyncCallback()
    {
        using var cancellation = new CancellationTokenSource();
        var dispatcher = new Mock<IDispatcher>(MockBehavior.Strict);
        Func<Task> callback = () => Task.CompletedTask;
        dispatcher.SetupGet(x => x.IsCurrent).Returns(false);
        dispatcher.Setup(x => x.InvokeAsync(callback, DispatchPriority.Middle, cancellation.Token)).Returns(Task.CompletedTask);
        Execute.Dispatcher = dispatcher.Object;
        await Execute.RunAsync(callback, DispatchPriority.Middle, cancellation.Token);
        dispatcher.VerifyAll();
    }
}
