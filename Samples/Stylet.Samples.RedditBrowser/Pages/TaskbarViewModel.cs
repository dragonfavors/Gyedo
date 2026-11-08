using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stylet.Samples.RedditBrowser.Events;
using Stylet.Samples.RedditBrowser.RedditApi;
using System;
using System.Collections.Generic;

namespace Stylet.Samples.RedditBrowser.Pages;

public partial class TaskbarViewModel : Screen
{
    private readonly IEventAggregator events;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpen))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    private string subreddit;

    public IEnumerable<SortMode> SortModes { get; }
    [ObservableProperty]
    private SortMode selectedSortMode;

    public TaskbarViewModel(IEventAggregator events)
    {
        this.events = events;
        this.SortModes = SortMode.AllModes;
        this.SelectedSortMode = SortMode.Hot;
    }

    public bool CanOpen => !string.IsNullOrWhiteSpace(this.Subreddit);
    [RelayCommand(CanExecute = nameof(CanOpen))]
    public void Open()
    {
        this.events.Publish(new OpenSubredditEvent() { Subreddit = this.Subreddit, SortMode = this.SelectedSortMode });
    }
}
