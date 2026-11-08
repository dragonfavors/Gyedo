using System.Threading;
using System.Threading.Tasks;
using Stylet.Samples.RedditBrowser.Events;
using System;

namespace Stylet.Samples.RedditBrowser.Pages;

public class ShellViewModel : Conductor<IScreen>.Collection.OneActive, IHandle<OpenSubredditEvent>
{
    private readonly ISubredditViewModelFactory subredditViewModelFactory;

    public TaskbarViewModel Taskbar { get; }

    public ShellViewModel(IEventAggregator events, TaskbarViewModel taskbarViewModel, ISubredditViewModelFactory subredditViewModelFactory)
    {
        this.DisplayName = "Reddit Browser";

        this.Taskbar = taskbarViewModel;
        this.subredditViewModelFactory = subredditViewModelFactory;

        events.Subscribe(this);
    }

    public async void Handle(OpenSubredditEvent message)
    {
        SubredditViewModel item = this.subredditViewModelFactory.CreateSubredditViewModel();
        item.Subreddit = message.Subreddit;
        item.SortMode = message.SortMode;
        try { await this.ActivateItemAsync(item); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Stylet.Logging.LogManager.GetLogger(this.GetType()).Error(exception); }
    }
}

public interface ISubredditViewModelFactory
{
    SubredditViewModel CreateSubredditViewModel();
}
