using System.Threading;
using Moq;
using NUnit.Framework;
using Stylet;
using System;
using System.Threading.Tasks;

namespace StyletUnitTests;

[TestFixture]
public class ConductorAllActiveTests
{
    public interface IMyScreen : IScreen, IDisposable
    { }

    private class MyConductor : Conductor<IScreen>.Collection.AllActive
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
    public async Task ActivateItemAddsItemToItems()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        Assert.That(this.conductor.Items, Is.EquivalentTo(new[] { screen.Object }));
    }

    [Test]
    public async Task ActivateItemDoesNotActivateIfConductorIsNotActive()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ActivateItemDeactivatesIfConductorIsNotActive()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.DeactivateAsync());
    }

    [Test]
    public async Task ActivateItemActivatesfConductorIsActive()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task ActivateItemDoesNotDeactivateIfConductorIsActive()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.DeactivateAsync(), Times.Never);
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
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);
        await ((IScreenState)this.conductor).CloseAsync();
        screen.Verify(x => x.CloseAsync());
        screen.Verify(x => x.Dispose());
        Assert.AreEqual(0, this.conductor.Items.Count);
    }

    [Test]
    public async Task ConductorCanCloseIfAllItemsCanClose()
    {
        var screen1 = ScreenMocks.Create<IScreen>();
        var screen2 = ScreenMocks.Create<IScreen>();

        screen1.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        screen2.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));

        await this.conductor.ActivateItemAsync(screen1.Object);
        await this.conductor.ActivateItemAsync(screen2.Object);
        Assert.IsTrue(this.conductor.CanCloseAsync().Result);
    }

    [Test]
    public async Task ConductorCanNotCloseIfAnyItemCanNotClose()
    {
        var screen1 = ScreenMocks.Create<IScreen>();
        var screen2 = ScreenMocks.Create<IScreen>();

        screen1.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        screen2.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(false));

        await this.conductor.ActivateItemAsync(screen1.Object);
        await this.conductor.ActivateItemAsync(screen2.Object);
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
    public async Task AddingItemActivatesAndSetsParent()
    {
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.VerifySet(x => x.Parent = this.conductor);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task RemovingItemClosesAndRemovesParent()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        screen.SetupGet(x => x.Parent).Returns(this.conductor);
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.VerifySet(x => x.Parent = null);
        screen.Verify(x => x.CloseAsync());
    }

    [Test]
    public async Task RemovingItemDisposesIfDisposeChildrenIsTrue()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.Verify(x => x.Dispose());
    }

    [Test]
    public async Task RemovingItemDoesNotDisposeIfDisposeChildrenIsFalse()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        this.conductor.DisposeChildren = false;
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.Verify(x => x.Dispose(), Times.Never);
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
        screen.Verify(x => x.Dispose());
        Assert.AreEqual(0, this.conductor.Items.Count);
    }

    [Test]
    public async Task ClosingConductorClosesAllItems()
    {
        var screen1 = ScreenMocks.Create<IMyScreen>();
        screen1.SetupGet(x => x.Parent).Returns(this.conductor);
        var screen2 = ScreenMocks.Create<IMyScreen>();
        screen2.SetupGet(x => x.Parent).Returns(this.conductor);
        await this.conductor.ActivateItemAsync(screen1.Object);
        await this.conductor.ActivateItemAsync(screen2.Object);

        await ((IScreenState)this.conductor).CloseAsync();
        screen1.Verify(x => x.CloseAsync());
        screen1.Verify(x => x.Dispose());
        screen1.VerifySet(x => x.Parent = null);
        screen2.Verify(x => x.CloseAsync());
        screen2.Verify(x => x.Dispose());
        screen2.VerifySet(x => x.Parent = null);
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
        CollectionAssert.DoesNotContain(this.conductor.Items, screen.Object);
    }

    [Test]
    public async Task AddRangeActivatesAddedItems()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);

        await this.conductor.ActivateItemAsync(screen.Object);

        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task RemoveRangeClosesRemovedItems()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);

        await this.conductor.CloseItemAsync(screen.Object);

        screen.Verify(x => x.CloseAsync());
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

        screen2.Verify(x => x.DeactivateAsync());
        screen2.Verify(x => x.CloseAsync());
        screen2.Verify(x => x.Dispose());
    }
}
