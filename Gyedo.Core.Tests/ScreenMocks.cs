using Moq;
using Gyedo;

namespace Gyedo.Wpf.Tests;

internal static class ScreenMocks
{
    public static Mock<T> Create<T>() where T : class, IGuardClose
    {
        var mock = new Mock<T>();
        mock.As<IGuardClose>().Setup(x => x.CanCloseAsync()).ReturnsAsync(true);
        return mock;
    }
}
