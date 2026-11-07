using System.Windows;
using System.Windows.Controls;

namespace Stylet;

public partial class MessageBoxView : Window
{
    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var model = (MessageBoxViewModel)this.DataContext;
        model.ButtonClicked((MessageBoxResult)button.Tag);
    }
}
