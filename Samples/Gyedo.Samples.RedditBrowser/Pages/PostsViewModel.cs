using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Gyedo.Samples.RedditBrowser.RedditApi;
using System;
using System.Diagnostics;

namespace Gyedo.Samples.RedditBrowser.Pages;

public partial class PostsViewModel : Screen
{
    public event EventHandler<PostCommentsOpenedEventArgs> PostCommentsOpened;

    private readonly IRedditClient client;

    [ObservableProperty]
    private string subreddit;
    [ObservableProperty]
    private SortMode sortMode;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNext), nameof(CanPrev))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(PrevCommand))]
    public partial PostCollection PostCollection { get; private set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenSelected), nameof(CanOpenSelectedComments))]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedCommand), nameof(OpenSelectedCommentsCommand))]
    private Post selectedPost;

    public PostsViewModel(IRedditClient client)
    {
        this.client = client;
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        var result = await this.client.GetPostsAsync(this.Subreddit, this.SortMode, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        this.PostCollection = result;
    }

    [RelayCommand]
    public async Task Close()
    {
        await this.RequestCloseAsync();
    }

    public bool CanNext => this.PostCollection != null && this.PostCollection.HasNext;
    [RelayCommand(CanExecute = nameof(CanNext))]
    public async Task NextAsync()
    {
        this.PostCollection = await this.PostCollection.NextAsync();
    }

    public bool CanPrev => this.PostCollection != null && this.PostCollection.HasPrev;
    [RelayCommand(CanExecute = nameof(CanPrev))]
    public async Task PrevAsync()
    {
        this.PostCollection = await this.PostCollection.PrevAsync();
    }

    public bool CanOpenSelected => this.SelectedPost != null;
    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    public void OpenSelected()
    {
        if (this.SelectedPost == null)
            return;

        Process.Start(this.SelectedPost.LinkUrl);
    }

    public bool CanOpenSelectedComments => this.SelectedPost != null && this.SelectedPost.NumComments > 0;
    [RelayCommand(CanExecute = nameof(CanOpenSelectedComments))]
    public void OpenSelectedComments()
    {
        if (this.SelectedPost == null)
            return;

        this.PostCommentsOpened?.Invoke(this, new PostCommentsOpenedEventArgs() { PostId36 = this.SelectedPost.Id36 });
    }
}

public class PostCommentsOpenedEventArgs : EventArgs
{
    public string PostId36 { get; set; }
}
