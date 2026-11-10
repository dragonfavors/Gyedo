using Moq;
using NUnit.Framework;
using Gyedo;
using Gyedo.Xaml;
using System.Windows;

namespace Gyedo.Wpf.Tests;

[TestFixture]
public class ApplicationLoaderTests
{
    private ApplicationLoader applicationLoader;

    [SetUp]
    public void SetUp()
    {
        // This will set up the URI registrations for the pack:// and application
        Application a = Application.Current;

        this.applicationLoader = new ApplicationLoader();
    }

    [Test]
    public void ConstructorSetsResourceDictionary()
    {
        Assert.AreEqual(1, this.applicationLoader.MergedDictionaries.Count);
        ResourceDictionary dict = this.applicationLoader.MergedDictionaries[0];
        Assert.AreEqual("pack://application:,,,/Gyedo.Wpf;component/Xaml/GyedoResourceDictionary.xaml", dict.Source.AbsoluteUri);
    }

    [Test]
    public void UnloadsResourceDictionaryIfRequested()
    {
        this.applicationLoader.LoadGyedoResources = false;
        Assert.AreEqual(0, this.applicationLoader.MergedDictionaries.Count);
    }

    [Test]
    public void CallsSetupOnBootstrapper()
    {
        var bootstrapper = new Mock<IBootstrapper>();
        this.applicationLoader.Bootstrapper = bootstrapper.Object;
        bootstrapper.Verify(x => x.Setup(Application.Current));
    }

    [Test]
    public void LoadGyedoResourcesReturnsCorrectValue()
    {
        Assert.True(this.applicationLoader.LoadGyedoResources);
        this.applicationLoader.LoadGyedoResources = false;
        Assert.False(this.applicationLoader.LoadGyedoResources);
    }

    [Test]
    public void BootstrapperReturnsCorrectValue()
    {
        Assert.Null(this.applicationLoader.Bootstrapper);
        var bootstrapper = new Mock<IBootstrapper>();
        this.applicationLoader.Bootstrapper = bootstrapper.Object;
        Assert.AreEqual(bootstrapper.Object, this.applicationLoader.Bootstrapper);
    }
}
