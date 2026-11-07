#if NETFRAMEWORK

using Moq;
using NUnit.Framework;
using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace StyletUnitTests;

[TestFixture, Apartment(ApartmentState.STA)]
public class MessageBoxViewTests
{
    [TestCase(MessageBoxResult.Yes)]
    [TestCase(MessageBoxResult.No)]
    [TestCase(MessageBoxResult.Cancel)]
    public void ButtonClickReturnsItsResultAndRequestsClose(MessageBoxResult result)
    {
        // Other converter tests initialize the dispatcher-bound singleton converters on
        // their own threads. Give this real XAML scenario its own singleton instances.
        var domain = AppDomain.CreateDomain("MessageBoxView test", null, AppDomain.CurrentDomain.SetupInformation);
        try
        {
            var scenario = (MessageBoxViewScenario)domain.CreateInstanceFromAndUnwrap(
                typeof(MessageBoxViewScenario).Assembly.Location, typeof(MessageBoxViewScenario).FullName);
            scenario.Run(result);
        }
        finally
        {
            AppDomain.Unload(domain);
        }
    }
}

public class MessageBoxViewScenario : MarshalByRefObject
{
    public void Run(MessageBoxResult result)
    {
        var parent = new Mock<IChildDelegate>();
        var model = new MessageBoxViewModel { Parent = parent.Object };
        model.Setup("Choose a result", buttons: MessageBoxButton.YesNoCancel);
        var view = new MessageBoxView();
        view.InitializeComponent();
        view.DataContext = model;

        try
        {
            var content = (FrameworkElement)view.Content;
            content.Measure(new Size(600, 400));
            content.Arrange(new Rect(0, 0, 600, 400));
            content.UpdateLayout();

            var buttons = Descendants(content).OfType<Button>().ToList();
            Assert.AreEqual(3, buttons.Count);
            Assert.AreEqual(MessageBoxResult.Yes, buttons.Single(x => x.IsDefault).Tag);
            Assert.AreEqual(MessageBoxResult.Cancel, buttons.Single(x => x.IsCancel).Tag);

            buttons.Single(x => Equals(x.Tag, result)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.AreEqual(result, model.ClickedButton);
            parent.Verify(x => x.CloseItem(model, true), Times.Once);
        }
        finally
        {
            view.Close();
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}

#endif
