using CommunityToolkit.Mvvm.Messaging;
using Moq;
using NUnit.Framework;
using Gyedo;
using Gyedo.Samples.RedditBrowser.Messages;
using Gyedo.Samples.RedditBrowser.Pages;
using Gyedo.Samples.RedditBrowser.RedditApi;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gyedo.Wpf.Tests;

[TestFixture]
public class RedditMessengerTests
{
    private readonly List<ShellViewModel> shells = new();
    private IDispatcher originalDispatcher;

    [SetUp]
    public void SetUp()
    {
        this.originalDispatcher = Execute.Dispatcher;
        Execute.Dispatcher = SynchronousDispatcher.Instance;
    }

    [TearDown]
    public async Task TearDown()
    {
        try
        {
            foreach (ShellViewModel shell in this.shells)
            {
                shell.AlwaysMessenger.UnregisterAll(shell);
                await shell.CloseAsync();
                await shell.ClearAsync();
            }
        }
        finally
        {
            this.shells.Clear();
            Execute.Dispatcher = this.originalDispatcher;
        }
    }

    private ShellViewModel CreateShell(IRedditClient client = null)
    {
        if (client == null)
        {
            var mock = new Mock<IRedditClient>();
            mock.Setup(x => x.GetPostsAsync(It.IsAny<string>(), It.IsAny<SortMode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PostCollection(Mock.Of<IRedditApi>(), "dotnet", "hot"));
            client = mock.Object;
        }
        var factory = new Mock<ISubredditViewModelFactory>();
        factory.Setup(x => x.CreateSubredditViewModel()).Returns(() =>
            new SubredditViewModel(new PostsViewModel(client), Mock.Of<IPostCommentsViewModelFactory>()));
        return this.CreateShell(factory.Object);
    }

    private ShellViewModel CreateShell(ISubredditViewModelFactory factory)
    {
        var shell = new ShellViewModel(new TaskbarViewModel(), factory);
        this.shells.Add(shell);
        return shell;
    }

    [Test]
    public async Task InstanceSubscriptionReceivesBeforeActivationAndAfterClose()
    {
        ShellViewModel shell = this.CreateShell();
        shell.Taskbar.Subreddit = "dotnet";
        shell.Taskbar.SelectedSortMode = SortMode.Top;
        await shell.Taskbar.OpenCommand.ExecuteAsync(null);
        var item = (SubredditViewModel)shell.ActiveItem;
        Assert.That(item.Subreddit, Is.EqualTo("dotnet"));
        Assert.That(item.SortMode, Is.EqualTo(SortMode.Top));
        Assert.That(item.IsActive, Is.False);

        await shell.ActivateAsync();
        await shell.DeactivateAsync();
        shell.Taskbar.Subreddit = "csharp";
        await shell.Taskbar.OpenCommand.ExecuteAsync(null);
        Assert.That(((SubredditViewModel)shell.ActiveItem).Subreddit, Is.EqualTo("csharp"));
        await shell.CloseAsync();
        Assert.That(shell.Items, Is.Empty);
        shell.Taskbar.Subreddit = "wpf";
        await shell.Taskbar.OpenCommand.ExecuteAsync(null);
        Assert.That(shell.ScreenState, Is.EqualTo(ScreenState.Closed));
        Assert.That(((SubredditViewModel)shell.ActiveItem).Subreddit, Is.EqualTo("wpf"));
    }

    [Test]
    public async Task OpenCommandWaitsForNavigationAndDisablesWhileLoading()
    {
        var gate = new TaskCompletionSource<PostCollection>();
        var client = new Mock<IRedditClient>();
        client.Setup(x => x.GetPostsAsync("dotnet", SortMode.Hot, It.IsAny<CancellationToken>()))
            .Returns((string _, SortMode _, CancellationToken token) => gate.Task.WaitAsync(token));
        ShellViewModel shell = this.CreateShell(client.Object);
        await shell.ActivateAsync();
        shell.Taskbar.Subreddit = "dotnet";
        Task opening = shell.Taskbar.OpenCommand.ExecuteAsync(null);
        Assert.That(opening.IsCompleted, Is.False);
        Assert.That(shell.Taskbar.OpenCommand.IsRunning, Is.True);
        Assert.That(shell.Taskbar.OpenCommand.CanExecute(null), Is.False);
        gate.SetResult(new PostCollection(Mock.Of<IRedditApi>(), "dotnet", "hot"));
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(shell.ActiveItem.IsActive, Is.True);
        Assert.That(shell.Taskbar.OpenCommand.IsRunning, Is.False);
        Assert.That(shell.Taskbar.OpenCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task MultipleShellsReplyOnceEachWithoutDuplicateRegistration()
    {
        ShellViewModel first = this.CreateShell();
        ShellViewModel second = this.CreateShell();
        await first.ActivateAsync();
        await first.DeactivateAsync();
        await first.ActivateAsync();
        var responses = await first.AlwaysMessenger.Send(new OpenSubredditRequest("dotnet", SortMode.New)).GetResponsesAsync();
        Assert.That(responses, Is.EqualTo(new[] { true, true }));
        Assert.That(first.Items.Count, Is.EqualTo(1));
        Assert.That(second.Items.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task FactoryFailureIsObservedAsAnUnsuccessfulResponse()
    {
        var factory = new Mock<ISubredditViewModelFactory>();
        factory.Setup(x => x.CreateSubredditViewModel()).Throws(new InvalidOperationException("factory failed"));
        ShellViewModel shell = this.CreateShell(factory.Object);
        var responses = await shell.AlwaysMessenger.Send(new OpenSubredditRequest("dotnet", SortMode.Hot)).GetResponsesAsync();
        Assert.That(responses, Is.EqualTo(new[] { false }));
        Assert.That(shell.Items, Is.Empty);
    }

    [Test]
    public async Task ClosingDuringNavigationCompletesTheResponseWithoutAsyncVoid()
    {
        var client = new Mock<IRedditClient>();
        client.Setup(x => x.GetPostsAsync(It.IsAny<string>(), It.IsAny<SortMode>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, SortMode _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return null;
            });
        ShellViewModel shell = this.CreateShell(client.Object);
        await shell.ActivateAsync();
        var replies = shell.AlwaysMessenger.Send(new OpenSubredditRequest("dotnet", SortMode.Hot)).GetResponsesAsync();
        Assert.That(replies.IsCompleted, Is.False);
        await shell.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(await replies.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(new[] { false }));
        Assert.That(shell.ScreenState, Is.EqualTo(ScreenState.Closed));
    }

    [Test]
    public async Task SendingWithoutAShellCompletesWithNoReplies()
    {
        var taskbar = new TaskbarViewModel { Subreddit = "dotnet" };
        var responses = await taskbar.AlwaysMessenger.Send(new OpenSubredditRequest("dotnet", SortMode.Hot)).GetResponsesAsync();
        Assert.That(responses, Is.Empty);
        await taskbar.OpenCommand.ExecuteAsync(null);
        Assert.That(taskbar.OpenCommand.IsRunning, Is.False);
    }
}
