using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Gyedo;

/// <summary>
/// <see cref="IDispatcher"/> implementation which can dispatch using <see cref="Dispatcher"/>
/// </summary>
public class ApplicationDispatcher : IDispatcher
{
    private readonly Dispatcher dispatcher;

    /// <summary>
    /// Initialises a new instance of the <see cref="ApplicationDispatcher"/> class with the given <see cref="Dispatcher"/>
    /// </summary>
    /// <param name="dispatcher"><see cref="Dispatcher"/> to use, normally Application.Current.Dispatcher</param>
    public ApplicationDispatcher(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ApplicationDispatcher"/> class with the given <see cref="Application"/>
    /// </summary>
    /// <param name="application"><see cref="Application"/> to use, normally Application</param>
    public ApplicationDispatcher(Application application)
        : this(application?.Dispatcher ?? throw new ArgumentNullException(nameof(application)))
    {
    }

    /// <inheritdoc/>
    public void Post(Action action, DispatchPriority priority = DispatchPriority.Lowest)
    {
        ArgumentNullException.ThrowIfNull(action);
        this.dispatcher.BeginInvoke(action, ToWpfPriority(priority));
    }

    /// <inheritdoc/>
    public void Send(Action action, DispatchPriority priority = DispatchPriority.Lowest)
    {
        ArgumentNullException.ThrowIfNull(action);
        DispatcherPriority nativePriority = ToWpfPriority(priority);
        // 같은 UI 스레드에서는 중첩 메시지 루프를 만들지 않고 직접 실행합니다.
        if (this.IsCurrent) action();
        else this.dispatcher.Invoke(action, nativePriority);
    }

    /// <inheritdoc/>
    public Task InvokeAsync(Action action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return AwaitOperationAsync(this.dispatcher.InvokeAsync(action, ToWpfPriority(priority), cancellationToken).Task);
    }

    /// <inheritdoc/>
    public Task<T> InvokeAsync<T>(Func<T> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return AwaitOperationAsync(this.dispatcher.InvokeAsync(action, ToWpfPriority(priority), cancellationToken).Task);
    }

    /// <inheritdoc/>
    public Task InvokeAsync(Func<Task> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return AwaitCallbackAsync(this.dispatcher.InvokeAsync(action, ToWpfPriority(priority), cancellationToken).Task);
    }

    /// <inheritdoc/>
    public Task<T> InvokeAsync<T>(Func<Task<T>> action, DispatchPriority priority = DispatchPriority.Lowest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return AwaitCallbackAsync(this.dispatcher.InvokeAsync(action, ToWpfPriority(priority), cancellationToken).Task);
    }

    private static DispatcherPriority ToWpfPriority(DispatchPriority priority) => priority switch
    {
        DispatchPriority.Lowest => DispatcherPriority.Background,
        DispatchPriority.Low => DispatcherPriority.Input,
        DispatchPriority.Middle => DispatcherPriority.Loaded,
        DispatchPriority.High => DispatcherPriority.Render,
        DispatchPriority.Highest => DispatcherPriority.Normal,
        _ => throw new ArgumentOutOfRangeException(nameof(priority)),
    };

    // WPF의 DispatcherOperation.Task를 기다려 게시가 중단된 경우도 취소 상태로 전달합니다.
    private static async Task AwaitOperationAsync(Task operation) => await operation.ConfigureAwait(false);
    private static async Task<T> AwaitOperationAsync<T>(Task<T> operation) => await operation.ConfigureAwait(false);

    private static async Task AwaitCallbackAsync(Task<Task> operation)
    {
        Task callback = await operation.ConfigureAwait(false);
        await (callback ?? throw new InvalidOperationException("콜백은 null 대신 Task를 반환해야 합니다.")).ConfigureAwait(false);
    }

    private static async Task<T> AwaitCallbackAsync<T>(Task<Task<T>> operation)
    {
        Task<T> callback = await operation.ConfigureAwait(false);
        return await (callback ?? throw new InvalidOperationException("콜백은 null 대신 Task를 반환해야 합니다.")).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public bool IsCurrent => this.dispatcher.CheckAccess();
}
