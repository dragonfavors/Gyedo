using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gyedo;

/// <summary>
/// UI 실행과 게시를 제공하는 편의 API입니다. Run 계열은 현재 UI에서 즉시 시작하고 InvokeAsync는 게시합니다.
/// </summary>
public static class Execute
{
    private static IDispatcher _dispatcher;

    /// <summary>
    /// Gets or sets Execute's dispatcher
    /// </summary>
    /// <remarks>
    /// UI 어댑터에서 현재 애플리케이션의 디스패처를 설정합니다.
    /// 설정 전에는 <see cref="SynchronousDispatcher.Instance"/>로 동기 실행합니다.
    /// </remarks>
    public static IDispatcher Dispatcher
    {
        get => _dispatcher ?? SynchronousDispatcher.Instance;
        set => _dispatcher = value ?? throw new ArgumentNullException(nameof(Dispatcher));
    }

    /// <summary>
    /// ValidatingModelBase와 Screen에서 사용하는 기본 속성 알림 디스패처입니다.
    /// 기본값은 호출한 스레드에서 동기 실행합니다.
    /// </summary>
    public static Action<Action> DefaultPropertyChangedDispatcher { get; set; }

    static Execute()
    {
        DefaultPropertyChangedDispatcher = a => a();
    }

    /// <summary>
    /// Action을 게시하고 반환합니다. 완료를 기다리려면 InvokeAsync를 사용합니다.
    /// </summary>
    /// <param name="action">Action to run on the UI thread</param>
    public static void Post(Action action)
        => Post(action, DispatchPriority.Lowest);

    /// <summary>지정한 우선순위로 게시합니다. 비동기 콜백은 InvokeAsync를 사용합니다.</summary>
    public static void Post(Action action, DispatchPriority priority)
    {
        SynchronousDispatcher.ValidateArguments(action, priority);
        Dispatcher.Post(action, priority);
    }

    /// <summary>
    /// Action을 게시하고 실행 완료까지 기다립니다. 현재 UI 스레드에서도 게시합니다.
    /// </summary>
    /// <remarks>UI 스레드를 Wait나 Result로 막지 말고 반환 Task를 await합니다.</remarks>
    /// <param name="action">Action to run on the UI thread</param>
    /// <param name="priority">게시 우선순위입니다.</param>
    /// <param name="cancellationToken">실행 전 게시를 취소하는 토큰입니다.</param>
    /// <returns>Task which completes when the action has been run</returns>
    public static Task InvokeAsync(Action action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        return Dispatcher.InvokeAsync(action, priority, cancellationToken);
    }

    /// <summary>
    /// 현재 UI이면 Action을 즉시 실행하고, 아니면 게시 후 반환합니다. 완료 대기는 RunAsync를 사용합니다.
    /// </summary>
    /// <param name="action">Action to run on the UI thread</param>
    public static void Run(Action action)
        => Run(action, DispatchPriority.Lowest);

    /// <summary>현재 UI 스레드이면 즉시 실행하고, 아니면 지정한 우선순위로 게시합니다.</summary>
    public static void Run(Action action, DispatchPriority priority)
    {
        SynchronousDispatcher.ValidateArguments(action, priority);
        IDispatcher dispatcher = Dispatcher;
        if (dispatcher.IsCurrent)
            action();
        else
            dispatcher.Post(action, priority);
    }

    /// <summary>
    /// UI에서 동기 실행하고 완료까지 호출 스레드를 대기시킵니다. 현재 UI이면 직접 실행합니다.
    /// </summary>
    /// <param name="action">Action to run on the UI thread</param>
    public static void Send(Action action)
        => Send(action, DispatchPriority.Lowest);

    /// <summary>UI에서 동기 실행합니다. 다른 스레드에서의 대기에만 우선순위를 적용합니다.</summary>
    public static void Send(Action action, DispatchPriority priority)
    {
        SynchronousDispatcher.ValidateArguments(action, priority);
        IDispatcher dispatcher = Dispatcher;
        Exception exception = null;
        if (dispatcher.IsCurrent)
        {
            action();
        }
        else
        {
            dispatcher.Send(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception = e;
                }
            }, priority);

            if (exception != null)
                throw new System.Reflection.TargetInvocationException("An error occurred while dispatching a call to the UI Thread", exception);
        }
    }

    /// <summary>
    /// 현재 UI이면 Action을 즉시 시작하고, 아니면 게시한 뒤 완료까지 기다립니다.
    /// </summary>
    /// <param name="action">Action to run on the UI thread</param>
    /// <param name="priority">게시 우선순위입니다. 현재 UI에서 즉시 실행할 때는 사용하지 않습니다.</param>
    /// <param name="cancellationToken">실행 전 취소 토큰입니다.</param>
    /// <returns>Task which completes when the action has been run</returns>
    public static Task RunAsync(Action action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        IDispatcher dispatcher = Dispatcher;
        return (dispatcher.IsCurrent ? SynchronousDispatcher.Instance : dispatcher).InvokeAsync(action, priority, cancellationToken);
    }

    /// <summary>콜백을 게시하고 반환값을 기다립니다.</summary>
    public static Task<T> InvokeAsync<T>(Func<T> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
        => Dispatcher.InvokeAsync(action, priority, cancellationToken);

    /// <summary>비동기 콜백을 게시하고 내부 Task 완료까지 기다립니다.</summary>
    public static Task InvokeAsync(Func<Task> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
        => Dispatcher.InvokeAsync(action, priority, cancellationToken);

    /// <summary>비동기 콜백을 게시하고 내부 Task의 결과를 기다립니다.</summary>
    public static Task<T> InvokeAsync<T>(Func<Task<T>> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
        => Dispatcher.InvokeAsync(action, priority, cancellationToken);

    /// <summary>현재 UI이면 즉시 시작하고, 아니면 게시한 뒤 반환값을 기다립니다.</summary>
    public static Task<T> RunAsync<T>(Func<T> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        IDispatcher dispatcher = Dispatcher;
        return (dispatcher.IsCurrent ? SynchronousDispatcher.Instance : dispatcher).InvokeAsync(action, priority, cancellationToken);
    }

    /// <summary>현재 UI이면 즉시 시작하고, 아니면 게시한 뒤 내부 Task 완료까지 기다립니다.</summary>
    public static Task RunAsync(Func<Task> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        IDispatcher dispatcher = Dispatcher;
        return (dispatcher.IsCurrent ? SynchronousDispatcher.Instance : dispatcher).InvokeAsync(action, priority, cancellationToken);
    }

    /// <summary>현재 UI이면 즉시 시작하고, 아니면 게시한 뒤 내부 Task의 결과를 기다립니다.</summary>
    public static Task<T> RunAsync<T>(Func<Task<T>> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        IDispatcher dispatcher = Dispatcher;
        return (dispatcher.IsCurrent ? SynchronousDispatcher.Instance : dispatcher).InvokeAsync(action, priority, cancellationToken);
    }
}
