using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Stylet.Samples.RedditBrowser.RedditApi;
using System;

namespace Stylet.Samples.RedditBrowser.Pages;

public partial class SubredditViewModel : Conductor<IScreen>.StackNavigation
{
    private readonly IPostCommentsViewModelFactory postCommentsViewModelFactory;

    [ObservableProperty]
    private string subreddit;
    [ObservableProperty]
    private SortMode sortMode;
    private readonly PostsViewModel posts;

    public SubredditViewModel(PostsViewModel posts, IPostCommentsViewModelFactory postCommentsViewModelFactory)
    {
        this.posts = posts;
        this.postCommentsViewModelFactory = postCommentsViewModelFactory;

        this.posts.PostCommentsOpened += async (o, e) =>
        {
            try { await this.OpenPostComments(e.PostId36); }
            catch (Exception exception) { Stylet.Logging.LogManager.GetLogger(this.GetType()).Error(exception); }
        };
    }

    protected override async Task OnInitialActivatedAsync(CancellationToken cancellationToken)
    {
        this.DisplayName = string.Format("/r/{0}", this.Subreddit);
        this.posts.Subreddit = this.Subreddit;
        this.posts.SortMode = this.SortMode;

        await this.ActivateItemAsync(this.posts);
    }

    private async Task OpenPostComments(string postId36)
    {
        PostCommentsViewModel item = this.postCommentsViewModelFactory.CreatePostCommentsViewModel();
        item.PostId36 = postId36;
        item.Subreddit = this.Subreddit;
        await this.ActivateItemAsync(item);
    }

    public override async Task<bool> CloseItemAsync(IScreen item)
    {
        bool closed = await base.CloseItemAsync(item);

        // If we're setting the ActiveItem to null, that means everything's been closed - close ourselves
        if (closed && this.ActiveItem == null)
            await this.RequestCloseAsync();
        return closed;
    }
}

public interface IPostCommentsViewModelFactory
{
    PostCommentsViewModel CreatePostCommentsViewModel();
}
