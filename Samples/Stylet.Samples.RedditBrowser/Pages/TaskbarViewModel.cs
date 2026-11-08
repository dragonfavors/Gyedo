using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Stylet.Samples.RedditBrowser.Messages;
using Stylet.Samples.RedditBrowser.RedditApi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stylet.Samples.RedditBrowser.Pages;

public partial class TaskbarViewModel : Screen
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpen))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    private string subreddit;

    public IEnumerable<SortMode> SortModes { get; }
    [ObservableProperty]
    private SortMode selectedSortMode;

    public TaskbarViewModel()
    {
        this.SortModes = SortMode.AllModes;
        this.SelectedSortMode = SortMode.Hot;
    }

    public bool CanOpen => !string.IsNullOrWhiteSpace(this.Subreddit);
    [RelayCommand(CanExecute = nameof(CanOpen))]
    public async Task OpenAsync()
    {
        var request = new OpenSubredditRequest(this.Subreddit, this.SelectedSortMode);
        await this.AlwaysMessenger.Send(request).GetResponsesAsync();
    }
}
