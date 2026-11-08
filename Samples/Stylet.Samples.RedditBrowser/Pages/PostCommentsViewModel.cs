using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stylet.Samples.RedditBrowser.RedditApi;
using System;

namespace Stylet.Samples.RedditBrowser.Pages;

public partial class PostCommentsViewModel : Screen
{
    private readonly IRedditClient client;

    [ObservableProperty]
    private string subreddit;
    [ObservableProperty]
    private string postId36;
    [ObservableProperty]
    public partial CommentCollection CommentCollection { get; private set; }

    public PostCommentsViewModel(IRedditClient client)
    {
        this.client = client;
    }

    protected override async void OnInitialActivate()
    {
        this.CommentCollection = await this.client.GetPostComments(this.Subreddit, this.PostId36);
    }

    [RelayCommand]
    public void GoBack()
    {
        this.RequestClose();
    }
}
