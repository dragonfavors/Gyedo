using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gyedo.Samples.RedditBrowser.RedditApi;
using System;

namespace Gyedo.Samples.RedditBrowser.Pages;

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

    protected override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        var result = await this.client.GetPostComments(this.Subreddit, this.PostId36, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        this.CommentCollection = result;
    }

    [RelayCommand]
    public async Task GoBack()
    {
        await this.RequestCloseAsync();
    }
}
