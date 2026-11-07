using System.Windows.Controls;
using System.Windows.Input;

namespace Stylet.Samples.RedditBrowser.Pages;

public partial class PostsView : UserControl
{
    private void OnPostDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (this.DataContext is PostsViewModel model && model.OpenSelectedCommand.CanExecute(null))
            model.OpenSelectedCommand.Execute(null);
    }
}
