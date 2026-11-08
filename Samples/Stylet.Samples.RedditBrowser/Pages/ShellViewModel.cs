using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;
using Stylet.Samples.RedditBrowser.Messages;
using System;

namespace Stylet.Samples.RedditBrowser.Pages;

public class ShellViewModel : Conductor<IScreen>.Collection.OneActive, IRecipient<OpenSubredditRequest>
{
    private readonly ISubredditViewModelFactory subredditViewModelFactory;

    public TaskbarViewModel Taskbar { get; }

    public ShellViewModel(TaskbarViewModel taskbarViewModel, ISubredditViewModelFactory subredditViewModelFactory)
    {
        this.DisplayName = "Reddit Browser";

        this.Taskbar = taskbarViewModel;
        this.subredditViewModelFactory = subredditViewModelFactory;
    }

    public void Receive(OpenSubredditRequest message) => message.Reply(this.OpenSubredditAsync(message));

    private async Task<bool> OpenSubredditAsync(OpenSubredditRequest message)
    {
        try
        {
            SubredditViewModel item = this.subredditViewModelFactory.CreateSubredditViewModel();
            item.Subreddit = message.Subreddit;
            item.SortMode = message.SortMode;
            return await this.ActivateItemAsync(item);
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception exception)
        {
            Stylet.Logging.LogManager.GetLogger(this.GetType()).Error(exception);
            return false;
        }
    }
}

public interface ISubredditViewModelFactory
{
    SubredditViewModel CreateSubredditViewModel();
}
