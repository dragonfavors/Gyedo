using Stylet.Examples;
using System.Windows.Input;
using Stylet.Samples.RedditBrowser.Events;
using Stylet.Samples.RedditBrowser.RedditApi;
using System;
using System.Collections.Generic;

namespace Stylet.Samples.RedditBrowser.Pages;

public class TaskbarViewModel : Screen
{
    private DelegateCommand openCommand;
    public ICommand OpenCommand => this.openCommand ?? (this.openCommand = new DelegateCommand(this, this.Open, () => this.CanOpen));

    private readonly IEventAggregator events;

    public string Subreddit { get; set; }

    public IEnumerable<SortMode> SortModes { get; private set; }
    public SortMode SelectedSortMode { get; set; }

    public TaskbarViewModel(IEventAggregator events)
    {
        this.events = events;
        this.SortModes = SortMode.AllModes;
        this.SelectedSortMode = SortMode.Hot;
    }

    public bool CanOpen => !string.IsNullOrWhiteSpace(this.Subreddit);
    public void Open()
    {
        this.events.Publish(new OpenSubredditEvent() { Subreddit = this.Subreddit, SortMode = this.SelectedSortMode });
    }
}
