using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Gyedo;
using System;

namespace Gyedo.Wpf.Tests;

[TestFixture]
public class ExecuteTests
{
    private static void CaptureAsync(Mock<IDispatcher> dispatcher, Action<Action> capture)
    {
        dispatcher.Setup(x => x.InvokeAsync(It.IsAny<Action>(), DispatchPriority.Lowest, CancellationToken.None))
            .Returns((Action action, DispatchPriority _, CancellationToken _) =>
            {
                var completion = new TaskCompletionSource();
                capture(() =>
                {
                    try { action(); completion.SetResult(); }
                    catch (Exception error) { completion.SetException(error); }
                });
                return completion.Task;
            });
    }

    private IDispatcher dispatcher;

    [SetUp]
    public void SetUp()
    {
        this.dispatcher = Execute.Dispatcher;
    }

    [TearDown]
    public void TearDown()
    {
        Execute.Dispatcher = this.dispatcher;
    }

    [Test]
    public void SendExecutesUsingDispatcherIfNotCurrent()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        Action passedAction = null;
        sync.Setup(x => x.Send(It.IsAny<Action>(), DispatchPriority.Lowest)).Callback<Action, DispatchPriority>((a, _) => passedAction = a);

        bool actionCalled = false;
        Execute.Send(() => actionCalled = true);

        Assert.IsFalse(actionCalled);
        passedAction();
        Assert.IsTrue(actionCalled);
    }

    [Test]
    public void SendExecutesSynchronouslyIfDispatcherIsCurrent()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        sync.SetupGet(x => x.IsCurrent).Returns(true);

        bool actionCalled = false;
        Execute.Send(() => actionCalled = true);

        Assert.IsTrue(actionCalled);
        sync.Verify(x => x.Send(It.IsAny<Action>(), DispatchPriority.Lowest), Times.Never);
    }

    [Test]
    public void PostExecutesUsingDispatcher()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        Action passedAction = null;
        sync.Setup(x => x.Post(It.IsAny<Action>(), DispatchPriority.Lowest)).Callback<Action, DispatchPriority>((a, _) => passedAction = a);

        bool actionCalled = false;
        Execute.Post(() => actionCalled = true);

        Assert.IsFalse(actionCalled);
        passedAction();
        Assert.IsTrue(actionCalled);
    }

    [Test]
    public void InvokeAsyncExecutesUsingDispatcher()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        Action passedAction = null;
        CaptureAsync(sync, action => passedAction = action);

        bool actionCalled = false;
        System.Threading.Tasks.Task task = Execute.InvokeAsync(() => { actionCalled = true; });

        Assert.IsFalse(task.IsCompleted);
        Assert.IsFalse(actionCalled);
        passedAction();
        Assert.IsTrue(actionCalled);
        Assert.IsTrue(task.IsCompleted);
    }

    [Test]
    public void RunExecutesUsingDispatcherIfNotCurrent()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        Action passedAction = null;
        sync.Setup(x => x.Post(It.IsAny<Action>(), DispatchPriority.Lowest)).Callback<Action, DispatchPriority>((a, _) => passedAction = a);

        bool actionCalled = false;
        Execute.Run(() => actionCalled = true);

        Assert.IsFalse(actionCalled);
        passedAction();
        Assert.IsTrue(actionCalled);
    }

    [Test]
    public void RunExecutesSynchronouslyIfCurrent()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        sync.SetupGet(x => x.IsCurrent).Returns(true);

        bool actionCalled = false;
        Execute.Run(() => actionCalled = true);

        Assert.IsTrue(actionCalled);
    }

    [Test]
    public void RunAsyncExecutesAsynchronouslyIfNotCurrent()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        Action passedAction = null;
        CaptureAsync(sync, action => passedAction = action);

        bool actionCalled = false;
        System.Threading.Tasks.Task task = Execute.RunAsync(() => { actionCalled = true; });

        Assert.IsFalse(task.IsCompleted);
        Assert.IsFalse(actionCalled);
        passedAction();
        Assert.IsTrue(actionCalled);
        Assert.IsTrue(task.IsCompleted);
    }

    [Test]
    public void RunAsyncExecutesSynchronouslyIfCurrent()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        sync.SetupGet(x => x.IsCurrent).Returns(true);

        bool actionCalled = false;
        System.Threading.Tasks.Task task = Execute.RunAsync(() => actionCalled = true);

        Assert.IsTrue(task.IsCompleted);
        Assert.IsTrue(actionCalled);
    }

    [Test]
    public void SendPropagatesException()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        var ex = new Exception("testy");
        sync.Setup(x => x.Send(It.IsAny<Action>(), DispatchPriority.Lowest)).Callback<Action, DispatchPriority>((a, _) => a());

        Exception caughtEx = null;
        try { Execute.Send(() => throw ex); }
        catch (Exception e) { caughtEx = e; }

        Assert.IsInstanceOf<System.Reflection.TargetInvocationException>(caughtEx);
        Assert.AreEqual(ex, caughtEx.InnerException);
    }

    [Test]
    public void RunAsyncPropagatesException()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        Action passedAction = null;
        CaptureAsync(sync, action => passedAction = action);

        var ex = new Exception("test");
        System.Threading.Tasks.Task task = Execute.RunAsync((Action)(() => throw ex));

        passedAction();
        Assert.IsTrue(task.IsFaulted);
        Assert.AreEqual(ex, task.Exception.InnerExceptions[0]);
    }

    [Test]
    public void InvokeAsyncPrepagatesException()
    {
        var sync = new Mock<IDispatcher>();
        Execute.Dispatcher = sync.Object;

        Action passedAction = null;
        CaptureAsync(sync, action => passedAction = action);

        var ex = new Exception("test");
        System.Threading.Tasks.Task task = Execute.InvokeAsync((Action)(() => throw ex));

        passedAction();
        Assert.IsTrue(task.IsFaulted);
        Assert.AreEqual(ex, task.Exception.InnerExceptions[0]);
    }

    [Test]
    public void ThrowsIfDispatcherSetToNull()
    {
        Assert.Throws<ArgumentNullException>(() => Execute.Dispatcher = null);
    }

    [Test]
    public void DefaultDispatcherIsSynchronous()
    {
        IDispatcher dispatcher = Execute.Dispatcher;

        Assert.IsTrue(dispatcher.IsCurrent);

        bool actionCalled = false;
        dispatcher.Post(() => actionCalled = true);
        Assert.IsTrue(actionCalled);

        actionCalled = false;
        dispatcher.Send(() => actionCalled = true);
        Assert.IsTrue(actionCalled);
    }

}
