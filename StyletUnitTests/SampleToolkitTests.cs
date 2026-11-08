using Moq;
using NUnit.Framework;
using Stylet;
using Stylet.Samples.ModelValidation;
using Stylet.Samples.ModelValidation.Pages;
using Stylet.Samples.RedditBrowser.Pages;
using Stylet.Samples.RedditBrowser.RedditApi;
using Stylet.Samples.RedditBrowser.RedditApi.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RedditPost = Stylet.Samples.RedditBrowser.RedditApi.Post;
using ResponsePost = Stylet.Samples.RedditBrowser.RedditApi.Contracts.Post;

namespace StyletUnitTests;

[TestFixture]
[NonParallelizable]
public class SampleToolkitTests
{
    private IDispatcher originalDispatcher;

    [SetUp]
    public void SetUp()
    {
        this.originalDispatcher = Execute.Dispatcher;
        Execute.Dispatcher = SynchronousDispatcher.Instance;
    }

    [TearDown]
    public void TearDown() => Execute.Dispatcher = this.originalDispatcher;

    [Test]
    public void SubredditInputUpdatesOpenButtonAndDependentProperty()
    {
        var model = new TaskbarViewModel(Mock.Of<IEventAggregator>());
        var properties = new List<string>();
        int commandChanges = 0;
        model.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        model.OpenCommand.CanExecuteChanged += (_, _) => commandChanges++;

        Assert.That(model.OpenCommand.CanExecute(null), Is.False);
        model.Subreddit = "dotnet";
        Assert.That(model.OpenCommand.CanExecute(null), Is.True);
        Assert.That(properties, Does.Contain(nameof(model.CanOpen)));
        model.Subreddit = " ";
        Assert.That(model.OpenCommand.CanExecute(null), Is.False);
        Assert.That(commandChanges, Is.EqualTo(2));
    }

    [Test]
    public void PostSelectionUpdatesBothCommandsAndPassesCommentId()
    {
        var model = new PostsViewModel(Mock.Of<IRedditClient>());
        int openChanges = 0, commentChanges = 0;
        string openedId = null;
        model.OpenSelectedCommand.CanExecuteChanged += (_, _) => openChanges++;
        model.OpenSelectedCommentsCommand.CanExecuteChanged += (_, _) => commentChanges++;
        model.PostCommentsOpened += (_, e) => openedId = e.PostId36;

        Assert.That(model.OpenSelectedCommand.CanExecute(null), Is.False);
        model.SelectedPost = new RedditPost { Id36 = "first", NumComments = 0 };
        Assert.That(model.OpenSelectedCommand.CanExecute(null), Is.True);
        Assert.That(model.OpenSelectedCommentsCommand.CanExecute(null), Is.False);
        model.SelectedPost = new RedditPost { Id36 = "second", NumComments = 1 };
        Assert.That(model.OpenSelectedCommentsCommand.CanExecute(null), Is.True);
        model.OpenSelectedCommentsCommand.Execute(null);
        Assert.That(openedId, Is.EqualTo("second"));
        model.SelectedPost = null;
        Assert.That(model.OpenSelectedCommand.CanExecute(null), Is.False);
        Assert.That(model.OpenSelectedCommentsCommand.CanExecute(null), Is.False);
        Assert.That(openChanges, Is.EqualTo(3));
        Assert.That(commentChanges, Is.EqualTo(3));
    }

    [Test]
    public async Task PaginationTracksAsyncExecutionAndRefreshesBothButtons()
    {
        var api = new Mock<IRedditApi>();
        api.Setup(x => x.FetchPostsAsync("dotnet", "hot"))
            .ReturnsAsync(Page(after: "next"));
        var pendingPage = new TaskCompletionSource<PostsResponse>();
        api.Setup(x => x.FetchNextPostsAsync("dotnet", "hot", "next", 0))
            .Returns(pendingPage.Task);
        api.Setup(x => x.FetchPrevPostsAsync("dotnet", "hot", "previous", 0))
            .ReturnsAsync(Page(after: "next"));
        var collection = new PostCollection(api.Object, "dotnet", "hot");
        await collection.LoadAsync();
        var client = new Mock<IRedditClient>();
        client.Setup(x => x.GetPostsAsync("dotnet", SortMode.Hot)).ReturnsAsync(collection);
        var model = new PostsViewModel(client.Object) { Subreddit = "dotnet", SortMode = SortMode.Hot };
        int previousChanges = 0;
        model.PrevCommand.CanExecuteChanged += (_, _) => previousChanges++;

        ((IScreenState)model).Activate();
        Assert.That(model.NextCommand.CanExecute(null), Is.True);
        Assert.That(model.PrevCommand.CanExecute(null), Is.False);
        Task execution = model.NextCommand.ExecuteAsync(null);
        Assert.That(model.NextCommand.IsRunning, Is.True);
        Assert.That(model.NextCommand.CanExecute(null), Is.False);
        pendingPage.SetResult(Page(before: "previous"));
        await execution;
        Assert.That(model.NextCommand.IsRunning, Is.False);
        Assert.That(model.NextCommand.CanExecute(null), Is.False);
        Assert.That(model.PrevCommand.CanExecute(null), Is.True);
        Assert.That(previousChanges, Is.GreaterThanOrEqualTo(2));
        await model.PrevCommand.ExecuteAsync(null);
        Assert.That(model.NextCommand.CanExecute(null), Is.True);
        Assert.That(model.PrevCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void GeneratedInputsRevalidatePasswordConfirmationAndSubmitState()
    {
        var model = NewUser();
        int changes = 0;
        model.SubmitCommand.CanExecuteChanged += (_, _) => changes++;
        Assert.That(model.SubmitCommand.CanExecute(null), Is.False);
        model.UserName = "user";
        model.Email = "user@example.com";
        model.Password = "password1";
        model.PasswordConfirmation = "password1";
        model.Age = 20;
        Assert.That(model.HasErrors, Is.False);
        Assert.That(model.SubmitCommand.CanExecute(null), Is.True);

        model.Password = "password2";
        Assert.That(model.GetErrors(nameof(model.PasswordConfirmation)).Cast<string>(), Is.Not.Empty);
        Assert.That(model.SubmitCommand.CanExecute(null), Is.False);
        Assert.That(changes, Is.GreaterThan(0));
    }

    [Test]
    public void AutoValidationToggleDispatchesSubmitButtonNotification()
    {
        var model = NewUser();
        var queued = new Queue<Action>();
        var dispatcher = new Mock<IDispatcher>();
        dispatcher.SetupGet(x => x.IsCurrent).Returns(false);
        dispatcher.Setup(x => x.Post(It.IsAny<Action>())).Callback<Action>(queued.Enqueue);
        Execute.Dispatcher = dispatcher.Object;
        int changes = 0;
        model.SubmitCommand.CanExecuteChanged += (_, _) => changes++;

        model.ShouldAutoValidate = false;
        Assert.That(model.SubmitCommand.CanExecute(null), Is.True);
        Assert.That(changes, Is.Zero);
        Assert.That(queued, Is.Not.Empty);
        while (queued.TryDequeue(out Action action))
            action();
        Assert.That(changes, Is.GreaterThan(0));
        model.ShouldAutoValidate = true;
        Assert.That(model.SubmitCommand.CanExecute(null), Is.False);
    }

    private static UserViewModel NewUser() => new(Mock.Of<IWindowManager>(),
        new FluentModelValidator<UserViewModel>(new UserViewModelValidator()));

    private static PostsResponse Page(string after = null, string before = null) => new()
    {
        Data = new PostsResponseData { After = after, Before = before, Children = new List<ResponsePost>() },
    };
}
