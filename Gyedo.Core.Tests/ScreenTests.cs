using System.Threading;
using Moq;
using NUnit.Framework;
using Gyedo;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gyedo.Wpf.Tests;

[TestFixture]
public class ScreenTests
{
    private class MyScreen : Screen
    {
        public new IModelValidator Validator
        {
            get => base.Validator;
            set => base.Validator = value;
        }

        public MyScreen() { }
        public MyScreen(IModelValidator validator) : base(validator) { }

        public void Reset()
        {
            this.OnActivateCalled = false;
            this.OnInitialActivateCalled = false;
            this.OnDeactivateCalled = false;
            this.OnCloseCalled = false;
        }


        public bool OnActivateCalled;
        protected override Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            this.OnActivateCalled = true;
            return Task.CompletedTask;
        }

        public bool OnInitialActivateCalled;
        protected override Task OnInitialActivatedAsync(CancellationToken cancellationToken)
        {
            this.OnInitialActivateCalled = true;
            return Task.CompletedTask;
        }

        public bool OnDeactivateCalled;
        protected override Task OnDeactivatedAsync()
        {
            this.OnDeactivateCalled = true;
            return Task.CompletedTask;
        }

        public bool OnCloseCalled;
        protected override Task OnClosedAsync()
        {
            this.OnCloseCalled = true;
            return Task.CompletedTask;
        }

        public ScreenState PreviousState;
        public ScreenState NewState;
        protected override Task OnStateChangedAsync(ScreenState oldState, ScreenState newState)
        {
            this.PreviousState = oldState;
            this.NewState = newState;
            return Task.CompletedTask;
        }

        public bool OnViewLoadedCalled;
        protected override Task OnViewLoadedAsync()
        {
            this.OnViewLoadedCalled = true;
            return Task.CompletedTask;
        }

        public bool? CanCloseResult = null;
        public override Task<bool> CanCloseAsync()
        {
            return this.CanCloseResult == null ? base.CanCloseAsync() : Task.FromResult(this.CanCloseResult.Value);
        }
    }

    private MyScreen screen;

    [SetUp]
    public void SetUp()
    {
        this.screen = new MyScreen();
    }

    [Test]
    public void SettingDisplayNameNotifies()
    {
        string changedProperty = null;
        this.screen.PropertyChanged += (o, e) => changedProperty = e.PropertyName;

        this.screen.DisplayName = "test";

        Assert.AreEqual("test", this.screen.DisplayName);
        Assert.AreEqual("DisplayName", changedProperty);
    }

    [Test]
    public void ScreenIsInitiallyNotActive()
    {
        Assert.IsFalse(this.screen.IsActive);
    }

    [Test]
    public void ScreenIsInitiallyDeactivated()
    {
        Assert.AreEqual(ScreenState.Deactivated, this.screen.ScreenState);
    }

    [Test]
    public async Task ScreenStateChangesNotifyStateAndActivity()
    {
        var properties = new List<string>();
        this.screen.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(Screen.ScreenState) || args.PropertyName == nameof(Screen.IsActive))
                properties.Add(args.PropertyName);
        };

        await this.screen.ActivateAsync();
        CollectionAssert.AreEquivalent(new[] { nameof(Screen.ScreenState), nameof(Screen.IsActive) }, properties);

        properties.Clear();
        await this.screen.DeactivateAsync();
        CollectionAssert.AreEquivalent(new[] { nameof(Screen.ScreenState), nameof(Screen.IsActive) }, properties);
    }

    [Test]
    public async Task ActivateActivatesIfNotAlreadyActive()
    {
        await this.screen.ActivateAsync();
        Assert.IsTrue(this.screen.IsActive);
    }

    [Test]
    public async Task ActivateFiresActivatedEvent()
    {
        bool fired = false;
        this.screen.Activated += (o, e) => fired = true;
        await this.screen.ActivateAsync();
        Assert.IsTrue(fired);
    }

    [Test]
    public async Task ActivateCallsOnActivate()
    {
        await this.screen.ActivateAsync();
        Assert.IsTrue(this.screen.OnActivateCalled);
    }

    [Test]
    public async Task DoubleActivationDoesntActivate()
    {
        await this.screen.ActivateAsync();
        this.screen.OnActivateCalled = false;
        await this.screen.ActivateAsync();
        Assert.IsFalse(this.screen.OnActivateCalled);
    }

    [Test]
    public async Task InitialActivationCallsOnInitialActivate()
    {
        await this.screen.ActivateAsync();
        this.screen.OnInitialActivateCalled = false;
        await this.screen.DeactivateAsync();
        await this.screen.ActivateAsync();
        Assert.IsFalse(this.screen.OnInitialActivateCalled);
    }

    [Test]
    public async Task ActivateFiresCorrectEvents()
    {
        // Get the initial activate out of the way
        await this.screen.ActivateAsync();
        await this.screen.DeactivateAsync();
        
        var changedEventArgs = new List<ScreenStateChangedEventArgs>();
        this.screen.StateChanged += (o, e) => changedEventArgs.Add(e);
        var activatedEventArgs = new List<ActivationEventArgs>();
        this.screen.Activated += (o, e) => activatedEventArgs.Add(e);

        await this.screen.ActivateAsync();

        Assert.AreEqual(1, changedEventArgs.Count);
        Assert.AreEqual(ScreenState.Active, changedEventArgs[0].NewState);
        Assert.AreEqual(ScreenState.Deactivated, changedEventArgs[0].PreviousState);

        Assert.AreEqual(ScreenState.Deactivated, this.screen.PreviousState);
        Assert.AreEqual(ScreenState.Active, this.screen.NewState);

        Assert.AreEqual(1, activatedEventArgs.Count);
        Assert.AreEqual(ScreenState.Deactivated, activatedEventArgs[0].PreviousState);
        Assert.IsFalse(activatedEventArgs[0].IsInitialActivate);
    }

    [Test]
    public async Task InitialActivateFiresCorrectEvents()
    {
        var changedEventArgs = new List<ScreenStateChangedEventArgs>();
        this.screen.StateChanged += (o, e) => changedEventArgs.Add(e);
        var activatedEventArgs = new List<ActivationEventArgs>();
        this.screen.Activated += (o, e) => activatedEventArgs.Add(e);

        await this.screen.ActivateAsync();

        Assert.AreEqual(1, changedEventArgs.Count);
        Assert.AreEqual(ScreenState.Active, changedEventArgs[0].NewState);
        Assert.AreEqual(ScreenState.Deactivated, changedEventArgs[0].PreviousState);

        Assert.AreEqual(ScreenState.Deactivated, this.screen.PreviousState);
        Assert.AreEqual(ScreenState.Active, this.screen.NewState);

        Assert.AreEqual(1, activatedEventArgs.Count);
        Assert.AreEqual(ScreenState.Deactivated, activatedEventArgs[0].PreviousState);
        Assert.IsTrue(activatedEventArgs[0].IsInitialActivate);
    }

    [Test]
    public async Task DeactivateDeactivates()
    {
        await this.screen.ActivateAsync(); ;
        await this.screen.DeactivateAsync();
        Assert.IsFalse(this.screen.IsActive);
    }

    [Test]
    public async Task DeactivateFiredDeactivatedEvent()
    {
        bool fired = false;
        this.screen.Deactivated += (o, e) => fired = true;
        await this.screen.ActivateAsync(); ;
        await this.screen.DeactivateAsync();
        Assert.IsTrue(fired);
    }

    [Test]
    public async Task DeactivateCallsOnDeactivate()
    {
        await this.screen.ActivateAsync();
        await this.screen.DeactivateAsync();
        Assert.IsTrue(this.screen.OnDeactivateCalled);
    }

    [Test]
    public async Task DoubleDeactivationDoesntDeactivate()
    {
        await this.screen.ActivateAsync();
        await this.screen.DeactivateAsync();
        this.screen.OnDeactivateCalled = false;
        await this.screen.DeactivateAsync();
        Assert.IsFalse(this.screen.OnDeactivateCalled);
    }

    [Test]
    public async Task DeactivateFiresCorrectEvents()
    {
        await this.screen.ActivateAsync();

        var changedEventArgs = new List<ScreenStateChangedEventArgs>();
        this.screen.StateChanged += (o, e) => changedEventArgs.Add(e);
        var deactivationEventArgs = new List<DeactivationEventArgs>();
        this.screen.Deactivated += (o, e) => deactivationEventArgs.Add(e);

        await this.screen.DeactivateAsync();

        Assert.AreEqual(1, changedEventArgs.Count);
        Assert.AreEqual(ScreenState.Deactivated, changedEventArgs[0].NewState);
        Assert.AreEqual(ScreenState.Active, changedEventArgs[0].PreviousState);

        Assert.AreEqual(ScreenState.Active, this.screen.PreviousState);
        Assert.AreEqual(ScreenState.Deactivated, this.screen.NewState);

        Assert.AreEqual(1, deactivationEventArgs.Count);
        Assert.AreEqual(ScreenState.Active, deactivationEventArgs[0].PreviousState);
    }

    [Test]
    public async Task CloseDeactivates()
    {
        await this.screen.ActivateAsync();
        await this.screen.CloseAsync();
        Assert.IsTrue(this.screen.OnDeactivateCalled);
    }

    [Test]
    public async Task CloseFiresClosed()
    {
        bool fired = false;
        this.screen.Closed += (o, e) => fired = true;
        await this.screen.CloseAsync();
        Assert.IsTrue(fired);
    }

    [Test]
    public async Task CloseCallsOnClose()
    {
        await this.screen.CloseAsync();
        Assert.IsTrue(this.screen.OnCloseCalled);
    }

    [Test]
    public async Task DoubleCloseDoesNotClose()
    {
        await this.screen.CloseAsync();
        this.screen.OnCloseCalled = false;
        await this.screen.CloseAsync();
        Assert.IsFalse(this.screen.OnCloseCalled);
    }

    [Test]
    public async Task CloseFiresCorrectEvents()
    {
        await this.screen.DeactivateAsync();

        var changedEventArgs = new List<ScreenStateChangedEventArgs>();
        this.screen.StateChanged += (o, e) => changedEventArgs.Add(e);
        var closeEventArgs = new List<CloseEventArgs>();
        this.screen.Closed += (o, e) => closeEventArgs.Add(e);

        await this.screen.CloseAsync();

        Assert.AreEqual(1, changedEventArgs.Count);
        Assert.AreEqual(ScreenState.Closed, changedEventArgs[0].NewState);
        Assert.AreEqual(ScreenState.Deactivated, changedEventArgs[0].PreviousState);

        Assert.AreEqual(ScreenState.Deactivated, this.screen.PreviousState);
        Assert.AreEqual(ScreenState.Closed, this.screen.NewState);

        Assert.AreEqual(1, closeEventArgs.Count);
        Assert.AreEqual(ScreenState.Deactivated, closeEventArgs[0].PreviousState);
    }

    [Test]
    public async Task ActivatingAllowsScreenToBeClosedAgain()
    {
        await this.screen.CloseAsync();
        this.screen.OnCloseCalled = false;
        await this.screen.ActivateAsync();
        await this.screen.CloseAsync();
        Assert.IsTrue(this.screen.OnCloseCalled);
    }

    [Test]
    public async Task ViewLoadedNotifiesWithoutActivatingScreen()
    {
        await ((IViewLifecycleAware)this.screen).ViewLoadedAsync();

        Assert.IsTrue(this.screen.OnViewLoadedCalled);
        Assert.AreEqual(ScreenState.Deactivated, this.screen.ScreenState);
        Assert.IsFalse(this.screen.OnActivateCalled);
    }

    [Test]
    public void SettingParentRaisesPropertyChange()
    {
        object parent = new();
        string changedProperty = null;
        this.screen.PropertyChanged += (o, e) => changedProperty = e.PropertyName;
        this.screen.Parent = parent;

        Assert.AreEqual(parent, this.screen.Parent);
        Assert.AreEqual("Parent", changedProperty);
    }

    [Test]
    public void CanCloseAsyncReturnsTrueByDefault()
    {
        Task<bool> task = this.screen.CanCloseAsync();
        Assert.IsTrue(task.IsCompleted);
        Assert.IsTrue(task.Result);
    }

    [Test]
    public void CanCloseAsyncCanRejectClosure()
    {
        this.screen.CanCloseResult = false;
        Task<bool> task = this.screen.CanCloseAsync();
        Assert.IsTrue(task.IsCompleted);
        Assert.IsFalse(task.Result);
    }

    [Test]
    public async Task RequestCloseThrowsIfParentIsNotIChildDelegate()
    {
        this.screen.Parent = new object();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await this.screen.RequestCloseAsync());
    }

    [Test]
    public async Task RequestCloseCallsParentCloseItemPassingDialogResult()
    {
        var parent = new Mock<IChildDelegate>();
        this.screen.Parent = parent.Object;
        await this.screen.RequestCloseAsync(true);
        parent.Verify(x => x.CloseItemAsync(this.screen, true));
    }

    [Test]
    public void PassesValidatorAdapter()
    {
        var adapter = new Mock<IModelValidator>();
        var screen = new MyScreen(adapter.Object);
        Assert.AreEqual(adapter.Object, screen.Validator);
    }

    [Test]
    public async Task InitialActivateFiredWhenComingFromDeactivated()
    {
        await this.screen.DeactivateAsync();
        await this.screen.ActivateAsync();
        Assert.True(this.screen.OnInitialActivateCalled);
    }

    [Test]
    public async Task ClosingResetsInitialActivate()
    {
        await this.screen.ActivateAsync();
        this.screen.OnInitialActivateCalled = false;
        await this.screen.CloseAsync();
        await this.screen.ActivateAsync();
        Assert.True(this.screen.OnInitialActivateCalled);
    }

    [Test]
    public async Task DeactivateAfterCloseIsNoOp()
    {
        await this.screen.ActivateAsync();
        await this.screen.CloseAsync();
        this.screen.Reset();

        await this.screen.DeactivateAsync();
        Assert.False(this.screen.OnInitialActivateCalled);
        Assert.False(this.screen.OnActivateCalled);
        Assert.False(this.screen.OnDeactivateCalled);
        Assert.That(this.screen.ScreenState, Is.EqualTo(ScreenState.Closed));
    }
}
