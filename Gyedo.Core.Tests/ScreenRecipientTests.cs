using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NUnit.Framework;
using Gyedo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Gyedo.Wpf.Tests;

[TestFixture]
public class ScreenRecipientTests
{
    public sealed record Ping;

    public sealed class RecipientScreen : Screen, IRecipient<Ping>
    {
        public RecipientScreen(IMessenger messenger) : base(null, messenger) { }
        public readonly List<(string Hook, ScreenState State, bool Active)> Seen = new();
        public int Messages;
        public Func<Task> Activation = () => Task.CompletedTask;
        public Func<Task> Deactivation = () => Task.CompletedTask;
        public Func<Task> Closure = () => Task.CompletedTask;
        public void Receive(Ping message) => this.Messages++;

        protected override Task OnInitialActivatedAsync(CancellationToken token)
        {
            this.Seen.Add(("initial", this.ScreenState, this.IsActive));
            this.Messenger.Send(new Ping());
            return Task.CompletedTask;
        }
        protected override Task OnActivatedAsync(CancellationToken token)
        {
            this.Seen.Add(("active", this.ScreenState, this.IsActive));
            this.Messenger.Send(new Ping());
            return this.Activation();
        }
        protected override Task OnDeactivatedAsync()
        {
            this.Seen.Add(("inactive", this.ScreenState, this.IsActive));
            this.Messenger.Send(new Ping());
            return this.Deactivation();
        }
        protected override Task OnClosedAsync()
        {
            this.Seen.Add(("closed", this.ScreenState, this.IsActive));
            return this.Closure();
        }
    }

    [Test]
    public async Task HooksSeeNewStateAndMessengerIsRegisteredOnlyWhileActive()
    {
        var messenger = new StrongReferenceMessenger();
        var screen = new RecipientScreen(messenger);
        Assert.That(screen, Is.InstanceOf<ObservableRecipient>());
        messenger.Send(new Ping());
        Assert.That(screen.Messages, Is.Zero);
        await screen.ActivateAsync();
        Assert.That(screen.Messages, Is.EqualTo(2), "초기·활성 훅 실행 전에 구독해야 합니다.");
        await screen.DeactivateAsync();
        messenger.Send(new Ping());
        Assert.That(screen.Messages, Is.EqualTo(2), "비활성 훅 실행 전에 구독을 해제해야 합니다.");
        await screen.ActivateAsync();
        Assert.That(screen.Messages, Is.EqualTo(3), "중복 등록 없이 다시 구독해야 합니다.");
        await screen.CloseAsync();
        Assert.That(messenger.IsRegistered<Ping>(screen), Is.False);
        Assert.That(screen.Seen, Is.EqualTo(new[]
        {
            ("initial", ScreenState.Active, true), ("active", ScreenState.Active, true),
            ("inactive", ScreenState.Deactivated, false), ("active", ScreenState.Active, true),
            ("inactive", ScreenState.Deactivated, false), ("closed", ScreenState.Closed, false),
        }));
    }

    [Test]
    public async Task ToolkitSetterCannotBypassAsyncLifecycleEvenThroughBaseReference()
    {
        var messenger = new StrongReferenceMessenger();
        var screen = new RecipientScreen(messenger);
        var recipient = (ObservableRecipient)screen;
        Assert.Throws<InvalidOperationException>(() => recipient.IsActive = true);
        Assert.That(screen.IsActive, Is.False);
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Deactivated));
        Assert.That(messenger.IsRegistered<Ping>(screen), Is.False);
        await screen.ActivateAsync();
        Assert.Throws<InvalidOperationException>(() => recipient.IsActive = false);
        Assert.That(screen.IsActive, Is.True);
        Assert.That(messenger.IsRegistered<Ping>(screen), Is.True);
        await screen.CloseAsync();
    }

    [Test]
    public async Task FailedActivationRollsBackStateAndUnregistersRecipient()
    {
        var messenger = new StrongReferenceMessenger();
        var screen = new RecipientScreen(messenger)
        {
            Activation = () => Task.FromException(new InvalidOperationException("failed")),
        };
        int activated = 0;
        screen.Activated += (_, _) => activated++;
        Assert.ThrowsAsync<InvalidOperationException>(async () => await screen.ActivateAsync());
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Deactivated));
        Assert.That(messenger.IsRegistered<Ping>(screen), Is.False);
        Assert.That(activated, Is.Zero);
        screen.Activation = () => Task.CompletedTask;
        await screen.ActivateAsync();
        Assert.That(activated, Is.EqualTo(1));
        await screen.CloseAsync();
    }

    [Test]
    public async Task FailedDeactivationRetriesCleanupBeforeReactivation()
    {
        var screen = new RecipientScreen(new StrongReferenceMessenger())
        {
            Deactivation = () => Task.FromException(new InvalidOperationException("cleanup failed")),
        };
        ScreenState? previous = null;
        screen.Deactivated += (_, e) => previous = e.PreviousState;
        await screen.ActivateAsync();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await screen.DeactivateAsync());
        Assert.That(screen.IsActive, Is.False);
        Assert.That(previous, Is.Null);
        screen.Deactivation = () => Task.CompletedTask;
        await screen.ActivateAsync();
        Assert.That(previous, Is.EqualTo(ScreenState.Active));
        Assert.That(screen.IsActive, Is.True);
        await screen.CloseAsync();
    }

    [Test]
    public async Task FailedCloseRetriesHookAndEmitsCompletionOnce()
    {
        var screen = new RecipientScreen(new StrongReferenceMessenger())
        {
            Closure = () => Task.FromException(new InvalidOperationException("cleanup failed")),
        };
        int events = 0;
        ScreenState? previous = null;
        screen.Closed += (_, e) => { events++; previous = e.PreviousState; };
        await screen.ActivateAsync();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await screen.CloseAsync());
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Closed));
        Assert.That(events, Is.Zero);
        screen.Closure = () => Task.CompletedTask;
        await screen.CloseAsync();
        await screen.CloseAsync();
        Assert.That(events, Is.EqualTo(1));
        Assert.That(previous, Is.EqualTo(ScreenState.Deactivated));
    }

    [Test]
    public async Task PropertyNotificationCannotReenterToolkitSetterAndDesynchronizeState()
    {
        var messenger = new StrongReferenceMessenger();
        var screen = new RecipientScreen(messenger);
        bool blocked = false;
        PropertyChangedEventHandler change = (_, e) =>
        {
            if (e.PropertyName == nameof(screen.IsActive) && screen.IsActive)
            {
                try { ((ObservableRecipient)screen).IsActive = false; }
                catch (InvalidOperationException) { blocked = true; }
            }
        };
        screen.PropertyChanged += change;
        await screen.ActivateAsync();
        Assert.That(blocked, Is.True);
        Assert.That(screen.IsActive, Is.True);
        Assert.That(screen.ScreenState, Is.EqualTo(ScreenState.Active));
        Assert.That(messenger.IsRegistered<Ping>(screen), Is.True);
        screen.PropertyChanged -= change;
        await screen.CloseAsync();
    }
}
