using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stylet;

/// <summary>
/// 게시·동기 실행·비동기 완료를 제공하는 UI 프레임워크 독립 계약입니다.
/// <see cref="Execute"/>에서 사용합니다.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Action을 게시하고 반환합니다. 완료를 기다리거나 비동기 콜백을 실행하려면 InvokeAsync를 사용합니다.
    /// </summary>
    /// <param name="action">Action to execute</param>
    /// <param name="priority">게시 우선순위입니다. 실행 중인 작업을 중단하지 않습니다.</param>
    void Post(Action action, DispatchPriority priority = DispatchPriority.Lowest);

    /// <summary>
    /// 대상 스레드에서 동기 실행하고 완료를 기다립니다. 같은 스레드에서는 직접 실행합니다.
    /// </summary>
    /// <param name="action">Action to execute</param>
    /// <param name="priority">다른 스레드에서 호출할 때 적용하는 게시 우선순위입니다.</param>
    void Send(Action action, DispatchPriority priority = DispatchPriority.Lowest);

    /// <summary>콜백을 게시하고 실행 완료·예외·실행 전 취소를 Task로 전달합니다.</summary>
    /// <remarks>UI 구현은 같은 UI 스레드에서도 게시합니다. 테스트용 SynchronousDispatcher는 즉시 실행합니다.</remarks>
    Task InvokeAsync(Action action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default);

    /// <summary>콜백을 게시하고 반환값을 전달합니다. 토큰은 실행 전 게시 취소에 사용합니다.</summary>
    Task<T> InvokeAsync<T>(Func<T> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default);

    /// <summary>콜백을 UI에서 시작하고 내부 Task의 완료·예외·취소까지 기다립니다.</summary>
    /// <remarks>실행 후 취소는 콜백이 협력해야 합니다. 우선순위는 최초 게시에 적용합니다.</remarks>
    Task InvokeAsync(Func<Task> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default);

    /// <summary>콜백을 UI에서 시작하고 내부 Task의 결과·예외·취소까지 기다립니다.</summary>
    Task<T> InvokeAsync<T>(Func<Task<T>> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the current thread is the thread being dispatched to
    /// </summary>
    bool IsCurrent { get; }
}

/// <summary>
/// UI가 없는 환경과 테스트에서 콜백을 현재 스레드에서 즉시 시작합니다.
/// 큐와 우선순위 정렬은 없지만 비동기 콜백의 완료·예외·취소는 그대로 전달합니다.
/// </summary>
public class SynchronousDispatcher : IDispatcher
{
    /// <summary>
    /// Gets the singleton instance of <see cref="SynchronousDispatcher"/>
    /// </summary>
    public static SynchronousDispatcher Instance { get; } = new SynchronousDispatcher();
    private SynchronousDispatcher() { }

    /// <inheritdoc/>
    public void Post(Action action, DispatchPriority priority = DispatchPriority.Lowest)
    {
        ValidateArguments(action, priority);
        action();
    }

    /// <inheritdoc/>
    public void Send(Action action, DispatchPriority priority = DispatchPriority.Lowest)
    {
        ValidateArguments(action, priority);
        action();
    }

    /// <inheritdoc/>
    public Task InvokeAsync(Action action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        ValidateArguments(action, priority);
        return RunAsync(() => { action(); return Task.CompletedTask; }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<T> InvokeAsync<T>(Func<T> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        ValidateArguments(action, priority);
        return RunAsync(() => Task.FromResult(action()), cancellationToken);
    }

    /// <inheritdoc/>
    public Task InvokeAsync(Func<Task> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        ValidateArguments(action, priority);
        return RunAsync(action, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<T> InvokeAsync<T>(Func<Task<T>> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        ValidateArguments(action, priority);
        return RunAsync(action, cancellationToken);
    }

    internal static void ValidateArguments(Delegate action, DispatchPriority priority)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (priority < DispatchPriority.Lowest || priority > DispatchPriority.Highest)
            throw new ArgumentOutOfRangeException(nameof(priority));
    }

    private static async Task RunAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await (action() ?? throw new InvalidOperationException("콜백은 null 대신 Task를 반환해야 합니다.")).ConfigureAwait(false);
    }

    private static async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await (action() ?? throw new InvalidOperationException("콜백은 null 대신 Task를 반환해야 합니다.")).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public bool IsCurrent => true;
}
