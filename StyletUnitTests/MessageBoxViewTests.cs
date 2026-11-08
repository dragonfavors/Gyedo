using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Stylet;
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
            parent.Verify(x => x.CloseItemAsync(model, true), Times.Once);
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
