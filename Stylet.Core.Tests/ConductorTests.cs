using System.Threading;
using Moq;
using NUnit.Framework;
using Stylet;
using System;
using System.Threading.Tasks;

namespace StyletUnitTests;

[TestFixture]
public class ConductorTests
{
    public interface IMyScreen : IScreen, IDisposable
    { }

    private class MyConductor : Conductor<IScreen>
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
    public void ActiveItemIsNullBeforeAnyItemsActivated()
    {
        Assert.IsNull(this.conductor.ActiveItem);
        Assert.That(this.conductor.GetChildren(), Is.Empty);
    }

    [Test]
    public async Task InitialActivateSetsItemAsActiveItem()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        Assert.AreEqual(screen.Object, this.conductor.ActiveItem);
    }

    [Test]
    public async Task InitialActivateDoesNotActivateItemIfConductorIsNotActive()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task InitialActivateActivatesItemIfConductorIsActive()
    {
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task ActivatesActiveItemWhenActivated()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()), Times.Never);

        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task DeactivatesActiveItemWhenDeactivated()
    {
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        await ((IScreenState)this.conductor).DeactivateAsync();
        screen.Verify(x => x.DeactivateAsync());
    }

    [Test]
    public async Task ActivateClosesPreviousItemIfConductorIsActiveAndPreviousItemCanClose()
    {
        var screen1 = ScreenMocks.Create<IMyScreen>();
        var screen2 = ScreenMocks.Create<IMyScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen1.Object);
        screen1.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        await this.conductor.ActivateItemAsync(screen2.Object);
        screen1.Verify(x => x.CloseAsync());
        screen1.Verify(x => x.Dispose());
    }

    [Test]
    public async Task ActivateDoesNothingIfPreviousItemCanNotClose()
    {
        var screen1 = ScreenMocks.Create<IMyScreen>();
        var screen2 = ScreenMocks.Create<IMyScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen1.Object);
        screen1.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(false));
        await this.conductor.ActivateItemAsync(screen2.Object);

        screen1.Verify(x => x.CloseAsync(), Times.Never);
        screen1.Verify(x => x.Dispose(), Times.Never);
        screen2.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ActivatingCurrentScreenReactivatesScreen()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        screen.Verify(x => x.CloseAsync(), Times.Never);
        screen.Verify(x => x.Dispose(), Times.Never);
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
    public async Task CloseItemDoesNothingIfToldToDeactiveInactiveItem()
    {
        var screen1 = ScreenMocks.Create<IMyScreen>();
        var screen2 = ScreenMocks.Create<IMyScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen1.Object);
        await this.conductor.CloseItemAsync(screen2.Object);

        screen1.Verify(x => x.CloseAsync(), Times.Never);
        screen1.Verify(x => x.Dispose(), Times.Never);
        screen2.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DeactiveDoesNotChangeActiveItem()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await ((IScreenState)this.conductor).ActivateAsync(CancellationToken.None);
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.DeactivateItemAsync(screen.Object);

        screen.Verify(x => x.DeactivateAsync());
        Assert.AreEqual(this.conductor.ActiveItem, screen.Object);
    }

    [Test]
    public async Task ActivateSetsConductorAsItemsParent()
    {
        var screen = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        screen.VerifySet(x => x.Parent = this.conductor);
    }

    [Test]
    public async Task CloseRemovesItemsParent()
    {
        var screen = ScreenMocks.Create<IScreen>();
        screen.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(true));
        screen.Setup(x => x.Parent).Returns(this.conductor);
        await this.conductor.ActivateItemAsync(screen.Object);
        await this.conductor.CloseItemAsync(screen.Object);
        screen.VerifySet(x => x.Parent = null);
    }
    
    [Test]
    public void CanCloseReturnsTrueIfNoActiveItem()
    {
        Assert.IsTrue(this.conductor.CanCloseAsync().Result);
    }

    [Test]
    public async Task CanCloseReturnsActiveItemsCanClose()
    {
        var screen1 = ScreenMocks.Create<IScreen>();
        await this.conductor.ActivateItemAsync(screen1.Object);
        screen1.Setup(x => x.CanCloseAsync()).Returns(Task.FromResult(false));
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
    public async Task ClosingConductorClosesActiveItem()
    {
        var screen1 = ScreenMocks.Create<IMyScreen>();
        screen1.SetupGet(x => x.Parent).Returns(this.conductor);
        await this.conductor.ActivateItemAsync(screen1.Object);
        await ((IScreenState)this.conductor).CloseAsync();
        screen1.Verify(x => x.CloseAsync());
        screen1.VerifySet(x => x.Parent = null);
    }

    [Test]
    public async Task ClosingConductorDisposesActiveItemIfDisposeChildrenIsTrue()
    {
        var screen = ScreenMocks.Create<IMyScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        await ((IScreenState)this.conductor).CloseAsync();
        screen.Verify(x => x.Dispose());
    }

    [Test]
    public async Task ClosingConductorDoesNotDisposeActiveItemIfDisposeChildrenIsFalse()
    {
        this.conductor.DisposeChildren = false;
        var screen = ScreenMocks.Create<IMyScreen>();
        await this.conductor.ActivateItemAsync(screen.Object);
        await ((IScreenState)this.conductor).CloseAsync();
        screen.Verify(x => x.Dispose(), Times.Never);
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
}
