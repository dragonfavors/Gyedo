using Microsoft.Extensions.DependencyInjection;
using Gyedo.Samples.RedditBrowser.Pages;
using Gyedo.Samples.RedditBrowser.RedditApi;

namespace Gyedo.Samples.RedditBrowser;

public class Bootstrapper : Bootstrapper<ShellViewModel>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IRedditClient, RedditClient>();
        services.AddTransient<TaskbarViewModel>();
        services.AddSingleton<ISubredditViewModelFactory, SubredditFactory>();
        services.AddSingleton<IPostCommentsViewModelFactory, CommentsFactory>();
    }

    private sealed class SubredditFactory(IRedditClient client, IPostCommentsViewModelFactory comments) : ISubredditViewModelFactory
    {
        public SubredditViewModel CreateSubredditViewModel() => new(new PostsViewModel(client), comments);
    }

    private sealed class CommentsFactory(IRedditClient client) : IPostCommentsViewModelFactory
    {
        public PostCommentsViewModel CreatePostCommentsViewModel() => new(client);
    }
}
