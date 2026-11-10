using NUnit.Framework;
using Gyedo;

namespace Gyedo.Wpf.Tests;

[TestFixture]
public class DesignModeTests
{
    [Test]
    public void IsEnabledIsOverridable()
    {
        bool previous = DesignMode.IsEnabled;
        try
        {
            DesignMode.IsEnabled = true;
            Assert.IsTrue(DesignMode.IsEnabled);

            DesignMode.IsEnabled = false;
            Assert.IsFalse(DesignMode.IsEnabled);
        }
        finally
        {
            DesignMode.IsEnabled = previous;
        }
    }
}
