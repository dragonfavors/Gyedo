using System.Windows;
using System.Windows.Controls;

namespace Stylet;

public partial class MessageBoxView : Window
{
    private async void OnButtonClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var model = (MessageBoxViewModel)this.DataContext;
        await model.ButtonClickedAsync((MessageBoxResult)button.Tag);
    }
}
