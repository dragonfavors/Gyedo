using CommunityToolkit.Mvvm.Messaging;
using NUnit.Framework;
using Stylet;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace StyletUnitTests;

[TestFixture]
public class ScreenMessengerLifetimeTests
{
    public sealed record LifetimeMessage(string Source);

    public sealed class LifetimeScreen : Screen, IRecipient<LifetimeMessage>
    {
        public IMessenger ActiveMessenger => this.Messenger;
        public readonly Dictionary<string, int> Received = new();
        public Func<CancellationToken, Task> Initialize = _ => Task.CompletedTask;
        public Func<Task> Deactivate = () => Task.CompletedTask;
        public Func<Task> Close = () => Task.CompletedTask;
        public int Initializations;
        public bool AllowClose = true;

        public void Receive(LifetimeMessage message) =>
            this.Received[message.Source] = this.Received.GetValueOrDefault(message.Source) + 1;

        protected override Task OnInitialActivatedAsync(CancellationToken token)
        {
            this.Initializations++;
            return this.Initialize(token);
        }
        protected override Task OnDeactivatedAsync() => this.Deactivate();
        protected override Task OnClosedAsync() => this.Close();
        public override Task<bool> CanCloseAsync() => Task.FromResult(this.AllowClose);
    }

    private static void AssertDelivery(LifetimeScreen screen, bool active, bool longer)
    {
        screen.Received.Clear();
        screen.ActiveMessenger.Send(new LifetimeMessage("active"));
        screen.LongMessenger.Send(new LifetimeMessage("long"));
        screen.AlwaysMessenger.Send(new LifetimeMessage("always"));
        Assert.Multiple(() =>
        {
            Assert.That(screen.Received.GetValueOrDefault("active"), Is.EqualTo(active ? 1 : 0));
            Assert.That(screen.Received.GetValueOrDefault("long"), Is.EqualTo(longer ? 1 : 0));
            Assert.That(screen.Received.GetValueOrDefault("always"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EachMessengerFollowsItsLifetimeWithoutDuplicateSubscriptions()
    {
        var screen = new LifetimeScreen();
        AssertDelivery(screen, active: false, longer: false);
        await screen.ActivateAsync();
        AssertDelivery(screen, active: true, longer: true);
        await screen.ActivateAsync();
        AssertDelivery(screen, active: true, longer: true);
        await screen.DeactivateAsync();
        AssertDelivery(screen, active: false, longer: true);
        await screen.ActivateAsync();
        AssertDelivery(screen, active: true, longer: true);
        Assert.That(screen.Initializations, Is.EqualTo(1));
        await screen.CloseAsync();
        await screen.CloseAsync();
        AssertDelivery(screen, active: false, longer: false);
        await screen.ActivateAsync();
        AssertDelivery(screen, active: true, longer: true);
        Assert.That(screen.Initializations, Is.EqualTo(2));
        await screen.CloseAsync();
    }

    [Test]
    public async Task MessengerInstancesAreSharedBetweenVmsAndSeparateBetweenLifetimes()
    {
        var sender = new Screen();
        var receiver = new LifetimeScreen();
        Assert.That(sender.LongMessenger, Is.SameAs(receiver.LongMessenger));
        Assert.That(sender.AlwaysMessenger, Is.SameAs(receiver.AlwaysMessenger));
        Assert.That(receiver.LongMessenger, Is.Not.SameAs(receiver.ActiveMessenger));
        Assert.That(receiver.AlwaysMessenger, Is.Not.SameAs(receiver.LongMessenger));
        Assert.That(receiver.AlwaysMessenger, Is.Not.SameAs(receiver.ActiveMessenger));
        sender.AlwaysMessenger.Send(new LifetimeMessage("instance"));
        Assert.That(receiver.Received["instance"], Is.EqualTo(1));
        await receiver.ActivateAsync();
        await receiver.DeactivateAsync();
        sender.LongMessenger.Send(new LifetimeMessage("session"));
        Assert.That(receiver.Received["session"], Is.EqualTo(1));
        await receiver.CloseAsync();
    }

    [Test]
    public async Task InitialHookStartsWithLongSubscriptionAndCancellationKeepsItUntilClose()
    {
        var screen = new LifetimeScreen
        {
            Initialize = token => Task.Delay(Timeout.Infinite, token),
        };
        Task activation = screen.ActivateAsync();
        Assert.That(activation.IsCompleted, Is.False);
        AssertDelivery(screen, active: true, longer: true);
        await screen.DeactivateAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.CatchAsync<OperationCanceledException>(async () => await activation);
        AssertDelivery(screen, active: false, longer: true);
        screen.Initialize = _ => Task.CompletedTask;
        await screen.ActivateAsync();
        AssertDelivery(screen, active: true, longer: true);
        Assert.That(screen.Initializations, Is.EqualTo(2));
        await screen.CloseAsync();
        AssertDelivery(screen, active: false, longer: false);
    }

    [Test]
    public async Task FailedInitializationCanRetryWithoutRegisteringLongRecipientTwice()
    {
        var screen = new LifetimeScreen
        {
            Initialize = _ => Task.FromException(new InvalidOperationException("initialization failed")),
        };
        Assert.ThrowsAsync<InvalidOperationException>(async () => await screen.ActivateAsync());
        AssertDelivery(screen, active: false, longer: true);
        screen.Initialize = _ => Task.CompletedTask;
        await screen.ActivateAsync();
        AssertDelivery(screen, active: true, longer: true);
        await screen.CloseAsync();
    }

    [Test]
    public async Task DeactivatedHookKeepsLongAndAlwaysSubscriptions()
    {
        var finish = new TaskCompletionSource();
        var screen = new LifetimeScreen { Deactivate = () => finish.Task };
        await screen.ActivateAsync();
        Task deactivation = screen.DeactivateAsync();
        Assert.That(deactivation.IsCompleted, Is.False);
        AssertDelivery(screen, active: false, longer: true);
        finish.SetResult();
        await deactivation;
        await screen.CloseAsync();
    }

    [Test]
    public async Task LongSubscriptionEndsBeforeCloseHookAndStaysOffIfCleanupFails()
    {
        var finish = new TaskCompletionSource();
        var screen = new LifetimeScreen { Close = () => finish.Task };
        await screen.ActivateAsync();
        Task close = screen.CloseAsync();
        Assert.That(close.IsCompleted, Is.False);
        AssertDelivery(screen, active: false, longer: false);
        finish.SetException(new InvalidOperationException("cleanup failed"));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await close);
        AssertDelivery(screen, active: false, longer: false);
        screen.Close = () => Task.CompletedTask;
        await screen.CloseAsync();
        await screen.ActivateAsync();
        AssertDelivery(screen, active: true, longer: true);
        await screen.CloseAsync();
    }

    [Test]
    public async Task RejectedClosePreservesAllSubscriptions()
    {
        var parent = new Conductor<Screen>();
        var screen = new LifetimeScreen { AllowClose = false };
        await parent.ActivateItemAsync(screen);
        await parent.ActivateAsync();
        Assert.That(await screen.RequestCloseAsync(), Is.False);
        AssertDelivery(screen, active: true, longer: true);
        screen.AllowClose = true;
        Assert.That(await screen.RequestCloseAsync(), Is.True);
        AssertDelivery(screen, active: false, longer: false);
        await parent.CloseAsync();
    }

    [Test]
    public async Task WeakMessengersDoNotKeepAnUnclosedInstanceAlive()
    {
        WeakReference reference = await CreateWeakReferenceAsync();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.That(reference.IsAlive, Is.False);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> CreateWeakReferenceAsync()
    {
        var screen = new LifetimeScreen();
        await screen.ActivateAsync();
        Assert.That(screen.LongMessenger.IsRegistered<LifetimeMessage>(screen), Is.True);
        Assert.That(screen.AlwaysMessenger.IsRegistered<LifetimeMessage>(screen), Is.True);
        var reference = new WeakReference(screen);
        screen = null;
        return reference;
    }
}
