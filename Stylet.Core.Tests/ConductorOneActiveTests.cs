using System.Threading;
using Moq;
using NUnit.Framework;
using Stylet;
using System;
using System.Threading.Tasks;

namespace StyletUnitTests;

[TestFixture]
public class ConductorOneActiveTests
{
    public interface IMyScreen : IScreen, IDisposable
    { }

    private class MyConductor : Conductor<IScreen>.Collection.OneActive
    {
        public bool CanCloseValue = true;
        public override async Task<bool> CanCloseAsync()
        {
            return this.CanCloseValue && await base.CanCloseAsync();
        }
    }

    private MyConductor conductor;

    [SetUp]
    public void SetUp()
    {
        this.conductor = new MyConductor();
    }

    [Test]
    public void NoActiveItemsBeforeAnyItemsActivated()
    {
        Assert.IsEmpty(this.conductor.Items);
    }

    [Test]
    public async Task ActivatingItemActivatesAndAddsToItems()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);

        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
        screen.Verify(x => x.DeactivateAsync(), Times.Never);
        Assert.AreEqual(screen.Object, this.conductor.ActiveItem);
        Assert.That(this.conductor.Items, Is.EquivalentTo(new[] { screen.Object }));
    }

    [Test]
    public async Task ActivatingItemDeactivatesPreviousItem()
    {
        var screen1 = ScreenMocks.Create<IScreen>();
        var screen2 = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);

        await this.conductor.ActivateItemAsync(screen1.Object);
        await this.conductor.ActivateItemAsync(screen2.Object);

        screen1.Verify(x => x.DeactivateAsync());
        screen2.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));

        Assert.AreEqual(screen2.Object, this.conductor.ActiveItem);
        Assert.AreEqual(new[] { screen1.Object, screen2.Object }, this.conductor.Items);
    }

    [Test]
    public async Task SettingActiveItemActivatesItem()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
        Assert.AreEqual(this.conductor.ActiveItem, screen.Object);
    }

    [Test]
    public async Task ClosingActiveItemChoosesPreviousItemIfAvailable()
    {
        var screen1 = ScreenMocks.Create<IScreen>();
        var screen2 = ScreenMocks.Create<IScreen>();
        var screen3 = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);

        await this.conductor.AddItemAsync(screen1.Object);
        await this.conductor.AddItemAsync(screen2.Object);
        await this.conductor.AddItemAsync(screen3.Object);
        await this.conductor.ActivateItemAsync(screen2.Object);

        screen2.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));

        await this.conductor.CloseItemAsync(screen2.Object);
        Assert.AreEqual(screen1.Object, this.conductor.ActiveItem);
        Assert.AreEqual(new[] { screen1.Object, screen3.Object }, this.conductor.Items);
    }

    [Test]
    public async Task ClosingActiveItemChoosesNextItemIfNoPreviousItem()
    {
        var screen1 = ScreenMocks.Create<IScreen>();
        var screen2 = ScreenMocks.Create<IScreen>();
        var screen3 = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);

        await this.conductor.AddItemAsync(screen1.Object);
        await this.conductor.AddItemAsync(screen2.Object);
        await this.conductor.AddItemAsync(screen3.Object);
        await this.conductor.ActivateItemAsync(screen3.Object);

        screen3.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));

        await this.conductor.CloseItemAsync(screen3.Object);
        Assert.AreEqual(screen2.Object, this.conductor.ActiveItem);
        Assert.AreEqual(new[] { screen1.Object, screen2.Object }, this.conductor.Items);
    }

    [Test]
    public async Task ActivateItemDoesNotActivateIfConductorIsNotActive()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ActivateItemDoesActiveIfConductorIsActive()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task DeactiveDeactivatesItems()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);
        await ((IScreenState)this.conductor).DeactivateAsync();
        screen.Verify(x => x.DeactivateAsync());
    }

    [Test]
    public async Task ClosingClosesAllItems()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        screen.Setup(x => x.CloseAsync()).Callback(() => System.Diagnostics.Debug.WriteLine("FOO"));

        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);
        await ((IScreenState)this.conductor).CloseAsync();
        screen.Verify(x => x.CloseAsync(), Times.Once());
        screen.Verify(x => x.Dispose(), Times.Once());
        Assert.AreEqual(0, this.conductor.Items.Count);
    }

    [Test]
    public async Task ConductorCanCloseIfAllItemsCanClose()
    {
        var screen1 = ScreenMocks.Create<IScreen>();
        var screen2 = ScreenMocks.Create<IScreen>();

        screen1.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        screen2.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));

        await this.conductor.AddItemAsync(screen1.Object);
        await this.conductor.AddItemAsync(screen2.Object);
        Assert.IsTrue(this.conductor.CanCloseAsync().Result);
    }

    [Test]
    public async Task ConductorCanNotCloseIfAnyItemCanNotClose()
    {
        var screen1 = ScreenMocks.Create<IScreen>();
        var screen2 = ScreenMocks.Create<IScreen>();

        screen1.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        screen2.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(false));

        await this.conductor.AddItemAsync(screen1.Object);
        await this.conductor.AddItemAsync(screen2.Object);
        Assert.IsFalse(this.conductor.CanCloseAsync().Result);
    }

    [Test]
    public async Task ConductorCanCloseAsyncCallsCanCloseOnSelfBeforeChildren()
    {
        var screen = ScreenMocks.Create<IScreen>();

        await this.conductor.ActivateItemAsync(screen.Object);
        this.conductor.CanCloseValue = false;

        Assert.IsFalse(this.conductor.CanCloseAsync().Result);

        screen.Verify(x => x.CanCloseAsync(), Times.Never());
    }

    [Test]
    public async Task AddingItemSetsParent()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.AddItemAsync(screen.Object);
        screen.VerifySet(x => x.Parent = this.conductor);
    }

    [Test]
    public async Task AddingItemDoesNotChangeActiveItem()
    {
        var screen1 = ScreenMocks.Create<IScreen>();
        var screen2 = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen1.Object);

        // This is an implementation detail
        screen1.Verify(x => x.DeactivateAsync(), Times.Never);
        screen1.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()), Times.Once);

        await this.conductor.AddItemAsync(screen2.Object);

        Assert.AreEqual(this.conductor.ActiveItem, screen1.Object);
        screen2.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()), Times.Never);
        screen1.Verify(x => x.DeactivateAsync(), Times.Never); // The one deactivate from earlier
    }

    [Test]
    public async Task RemovingItemClosesAndRemovesParent()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        screen.SetupGet(x => x.Parent).Returns(this.conductor);
        await this.conductor.AddItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.VerifySet(x => x.Parent = null);
        screen.Verify(x => x.CloseAsync());
    }

    [Test]
    public async Task RemovingItemDisposesIfDisposeChildrenIsTrue()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        await this.conductor.AddItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.Verify(x => x.Dispose());
    }

    [Test]
    public async Task RemovingItemDoesNotDisposeIfDisposeChildrenIsFalse()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        this.conductor.DisposeChildren = false;
        await this.conductor.AddItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.Verify(x => x.Dispose(), Times.Never);
    }

    [Test]
    public async Task RemovingActiveItemActivatesAnotherItem()
    {
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        var screen1 = ScreenMocks.Create<IMyScreen>();
        var screen2 = ScreenMocks.Create<IMyScreen>();
        await this.conductor.ActivateItemAsync(screen1.Object);
        await this.conductor.AddItemAsync(screen2.Object);

        await this.conductor.CloseItemAsync(screen1.Object);

        Assert.AreEqual(this.conductor.ActiveItem, screen2.Object);
        screen2.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
        screen1.Verify(x => x.CloseAsync());
    }

    [Test]
    public async Task AddingItemTwiceDoesNotResultInDuplicates()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.ActivateItemAsync(screen.Object);
        Assert.AreEqual(1, this.conductor.Items.Count);
    }

    [Test]
    public async Task DeactivateItemDeactivatesItemButDoesNotRemoveFromItems()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.DeactivateItemAsync(screen.Object);
        screen.Verify(x => x.DeactivateAsync());
        Assert.That(this.conductor.Items, Is.EquivalentTo(new[] { screen.Object }));
    }

    [Test]
    public async Task CloseItemDoesNothingIfItemCanNotClose()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Setup(x => x.CanCloseAsync()).ReturnsAsync(false);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.Verify(x => x.CloseAsync(), Times.Never);
        screen.Verify(x => x.Dispose(), Times.Never);
        Assert.That(this.conductor.Items, Is.EquivalentTo(new[] { screen.Object }));
    }

    [Test]
    public async Task CloseItemDeactivatesItemAndRemovesFromItemsIfItemCanClose()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        screen.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.Verify(x => x.CloseAsync());
        Assert.AreEqual(0, this.conductor.Items.Count);
    }

    [Test]
    public async Task CloseItemDisposesIfDisposeChildrenIsTrue()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        screen.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.Verify(x => x.Dispose());
    }

    [Test]
    public async Task CloseItemDoesNotDisposeIfDisposeChildrenIsFalse()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        this.conductor.DisposeChildren = false;
        screen.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.Verify(x => x.Dispose(), Times.Never);
    }

    [Test]
    public async Task ClosingConductorClosesActiveItem()
    {
        var screen1 = ScreenMocks.Create<IMyScreen>();
        screen1.SetupGet(x => x.Parent).Returns(this.conductor);
        await this.conductor.ActivateItemAsync(screen1.Object);
        await ((IScreenState)this.conductor).CloseAsync();
        screen1.Verify(x => x.CloseAsync());
        screen1.Verify(x => x.Dispose());
        screen1.VerifySet(x => x.Parent = null);
    }

    [Test]
    public async Task ClosesItemIfItemRequestsClose()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        await ((IChildDelegate)this.conductor).CloseItemAsync(screen.Object);

        screen.Verify(x => x.CloseAsync());
        screen.Verify(x => x.Dispose());
        Assert.Null(this.conductor.ActiveItem);
    }

    [Test]
    public async Task ClearingItemsClosesAndDisposes()
    {
        var screen1 = ScreenMocks.Create<IMyScreen>();
        var screen2 = ScreenMocks.Create<IMyScreen>();

        await this.conductor.ActivateItemAsync(screen1.Object);
        await this.conductor.ActivateItemAsync(screen2.Object);

        await this.conductor.ClearAsync();

        screen1.Verify(x => x.DeactivateAsync());
        screen1.Verify(x => x.CloseAsync());
        screen1.Verify(x => x.Dispose());

        screen2.Verify(x => x.CloseAsync());
        screen2.Verify(x => x.Dispose());
    }

    [Test]
    public async Task ClosingDoesNotCloseThenDeactivateActiveItem()
    {
        var screen = new AsyncScreenProbe();
        await this.conductor.ActivateItemAsync(screen);
        await this.conductor.ActivateAsync();
        screen.Calls.Clear();
        await this.conductor.CloseAsync();
        Assert.That(screen.Calls, Is.EqualTo(new[] { "deactivate", "close", "dispose" }));
    }

    [Test]
    public async Task ReplacingActiveItemDoesNotCloseThenDeactivate()
    {
        var screen = new AsyncScreenProbe();
        await this.conductor.ActivateItemAsync(screen);
        await this.conductor.ActivateAsync();
        screen.Calls.Clear();
        await this.conductor.CloseItemAsync(screen);
        Assert.That(screen.Calls, Is.EqualTo(new[] { "deactivate", "close", "dispose" }));
    }

    [Test]
    public async Task RemovingActiveItemDoesNotCloseThenDeactivate()
    {
        var screen = new AsyncScreenProbe();
        await this.conductor.ActivateItemAsync(screen);
        await this.conductor.ActivateAsync();
        screen.Calls.Clear();
        await this.conductor.CloseItemAsync(screen);
        Assert.That(screen.Calls, Is.EqualTo(new[] { "deactivate", "close", "dispose" }));
    }

    [Test]
    public async Task ClosingTheActiveItemDoesNotDisposeItTwice()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        screen.Setup(x => x.CanCloseAsync()).ReturnsAsync(true);

        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);

        screen.Verify(x => x.Dispose(), Times.Once());
    }

    [Test]
    public async Task ClosingTheNonActiveItemDoesNotDisposeItTwice()
    {
        var screen1 = ScreenMocks.Create<IMyScreen>();
        var screen2 = ScreenMocks.Create<IMyScreen>();
        screen1.Setup(x => x.CanCloseAsync()).ReturnsAsync(true);

        await this.conductor.ActivateItemAsync(screen1.Object);
        await this.conductor.ActivateItemAsync(screen2.Object);
        await this.conductor.CloseItemAsync(screen1.Object);

        screen1.Verify(x => x.Dispose(), Times.Once());
    }
}

