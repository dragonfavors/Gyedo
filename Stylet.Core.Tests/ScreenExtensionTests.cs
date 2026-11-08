using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Stylet;
using System;

namespace StyletUnitTests;

[TestFixture]
public class ScreenExtensionTests
{
    public interface IMyScreen : IScreen, IDisposable
    { }

    private Screen parent;
    private Mock<IMyScreen> child;

    [SetUp]
    public void SetUp()
    {
        this.parent = new Screen();
        this.child = new Mock<IMyScreen>();
    }

    [Test]
    public async Task TryActivateActivatesIScreenState()
    {
        var screen = new Mock<IScreenState>();
        await ScreenExtensions.TryActivateAsync(screen.Object);
        screen.Verify(x => x.ActivateAsync(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task TryActivateDoesNothingToNonIScreenState()
    {
        var screen = new Mock<IGuardClose>(MockBehavior.Strict);
        await ScreenExtensions.TryActivateAsync(screen.Object);
    }

    [Test]
    public async Task TryDeactivateDeactivatesIScreenState()
    {
        var screen = new Mock<IScreenState>();
        await ScreenExtensions.TryDeactivateAsync(screen.Object);
        screen.Verify(x => x.DeactivateAsync());
    }

    [Test]
    public async Task TryDeactivateDoesNothingToNonIScreenState()
    {
        var screen = new Mock<IGuardClose>(MockBehavior.Strict);
        await ScreenExtensions.TryDeactivateAsync(screen.Object);
    }

    [Test]
    public async Task TryCloseClosesIScreenState()
    {
        var screen = new Mock<IScreenState>();
        await ScreenExtensions.TryCloseAsync(screen.Object);
        screen.Verify(x => x.CloseAsync());
    }

    [Test]
    public async Task TryDisposeDisposesIDisposable()
    {
        var screen = new Mock<IDisposable>();
        await ScreenExtensions.TryDisposeAsync(screen.Object);
        screen.Verify(x => x.Dispose());
    }

    [Test]
    public async Task TryCloseDoesNothingToNonIScreenState()
    {
        var screen = new Mock<IGuardClose>(MockBehavior.Strict);
        await ScreenExtensions.TryCloseAsync(screen.Object);
    }


}
