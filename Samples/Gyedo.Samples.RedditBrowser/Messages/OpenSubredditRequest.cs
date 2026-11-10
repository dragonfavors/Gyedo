using CommunityToolkit.Mvvm.Messaging.Messages;
using Gyedo.Samples.RedditBrowser.RedditApi;

namespace Gyedo.Samples.RedditBrowser.Messages;

// 각 Shell의 탐색 결과를 받아 모든 수신자의 작업 완료를 기다립니다.
public sealed class OpenSubredditRequest(string subreddit, SortMode sortMode) : AsyncCollectionRequestMessage<bool>
{
    public string Subreddit { get; } = subreddit;
    public SortMode SortMode { get; } = sortMode;
}
