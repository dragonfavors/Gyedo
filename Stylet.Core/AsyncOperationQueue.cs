using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stylet;

// 호출 순서를 보존하고, 같은 훅에서 자기 큐를 기다리는 재진입은 교착 대신 예외로 알립니다.
internal sealed class AsyncOperationQueue
{
    private readonly object sync = new();
    private readonly AsyncLocal<ExecutionScope> executing = new();
    private Task tail = Task.CompletedTask;

    public Task RunAsync(Func<Task> operation) => this.RunAsync(async () =>
    {
        await operation();
        return true;
    });

    public Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        if (this.executing.Value?.Active == true)
            return Task.FromException<T>(new InvalidOperationException(
                "수명주기 훅에서 같은 화면의 상태 전환을 다시 기다릴 수 없습니다."));
        Task previous;
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this.sync)
        {
            previous = this.tail;
            this.tail = finished.Task;
        }
        return this.ExecuteAsync(previous, operation, finished);
    }

    private async Task<T> ExecuteAsync<T>(Task previous, Func<Task<T>> operation, TaskCompletionSource finished)
    {
        await previous;
        ExecutionScope previousScope = this.executing.Value;
        var scope = new ExecutionScope();
        this.executing.Value = scope;
        try { return await operation(); }
        finally
        {
            scope.Active = false;
            this.executing.Value = previousScope;
            finished.SetResult();
        }
    }

    private sealed class ExecutionScope
    {
        public volatile bool Active = true;
    }
}
