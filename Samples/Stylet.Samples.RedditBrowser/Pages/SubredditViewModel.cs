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

        this.posts.PostCommentsOpened += (o, e) => this.OpenPostComments(e.PostId36);
    }

    protected override void OnInitialActivate()
    {
        this.DisplayName = string.Format("/r/{0}", this.Subreddit);
        this.posts.Subreddit = this.Subreddit;
        this.posts.SortMode = this.SortMode;

        this.ActivateItem(this.posts);
    }

    private void OpenPostComments(string postId36)
    {
        PostCommentsViewModel item = this.postCommentsViewModelFactory.CreatePostCommentsViewModel();
        item.PostId36 = postId36;
        item.Subreddit = this.Subreddit;
        this.ActivateItem(item);
    }

    protected override void ChangeActiveItem(IScreen newItem, bool closePrevious)
    {
        base.ChangeActiveItem(newItem, closePrevious);

        // If we're setting the ActiveItem to null, that means everything's been closed - close ourselves
        if (newItem == null)
            this.RequestClose();
    }
}

public interface IPostCommentsViewModelFactory
{
    PostCommentsViewModel CreatePostCommentsViewModel();
}
