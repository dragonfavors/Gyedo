using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Gyedo;

/// <summary>초기화·활성화·비활성화·종료 완료를 기다릴 수 있는 화면 모델입니다.</summary>
public class Screen : ValidatingModelBase, IScreen, IViewLifecycleAware
{
    private static readonly WeakReferenceMessenger defaultLongMessenger = new();
    private static readonly WeakReferenceMessenger defaultAlwaysMessenger = new();
    private readonly AsyncOperationQueue lifecycle = new();
    private CancellationTokenSource activationCancellation;
    private long activationVersion;
    private bool initialized;
    private bool longMessengerRegistered;
    private bool deactivationPending;
    private bool closePending;
    private ScreenState deactivationPreviousState;
    private ScreenState closePreviousState;
    private bool settingRecipientState;
    private string displayName;
    private object parent;
    private ScreenState screenState = ScreenState.Deactivated;

    /// <summary>검증기 없이 화면을 생성합니다.</summary>
    public Screen() : this(null, WeakReferenceMessenger.Default) { }
    /// <summary>검증기를 연결해 화면을 생성합니다.</summary>
    public Screen(IModelValidator validator) : this(validator, WeakReferenceMessenger.Default) { }
    /// <summary>검증기와 메시지 전달자를 연결합니다.</summary>
    public Screen(IModelValidator validator, IMessenger messenger) : base(validator, messenger)
    {
        this.DisplayName = this.GetType().FullName;
        this.AlwaysMessenger.RegisterAll(this);
    }

    /// <summary>
    /// 최초 활성화 훅 실행 전부터 종료 훅 실행 전까지 수신하는 공용 약한 참조 메신저입니다.
    /// 비활성화와 활성화 실패·취소에서는 구독을 유지하고, 닫았다 다시 열면 재등록합니다.
    /// </summary>
    public IMessenger LongMessenger => defaultLongMessenger;

    /// <summary>
    /// 생성자에서 수신을 등록하는 공용 약한 참조 메신저입니다. 비활성화·종료 후에도 수신합니다.
    /// 메신저는 인스턴스를 살려 두지 않으므로 외부 참조가 사라지면 가비지 수집할 수 있습니다.
    /// </summary>
    public IMessenger AlwaysMessenger => defaultAlwaysMessenger;
    /// <inheritdoc/>
    public string DisplayName { get => this.displayName; set => this.SetProperty(ref this.displayName, value); }
    /// <inheritdoc/>
    public object Parent { get => this.parent; set => this.SetProperty(ref this.parent, value); }
    /// <inheritdoc/>
    public ScreenState ScreenState => this.screenState;
    /// <inheritdoc/>
    public new bool IsActive => base.IsActive;
    /// <summary>현재 활성화의 토큰입니다. 비활성화·종료 요청 시 취소됩니다.</summary>
    protected CancellationToken ActivationCancellationToken =>
        this.activationCancellation?.Token ?? new CancellationToken(true);

    /// <inheritdoc/>
    public event EventHandler<ScreenStateChangedEventArgs> StateChanged;
    /// <inheritdoc/>
    public event EventHandler<ActivationEventArgs> Activated;
    /// <inheritdoc/>
    public event EventHandler<DeactivationEventArgs> Deactivated;
    /// <inheritdoc/>
    public event EventHandler<CloseEventArgs> Closed;
    /// <inheritdoc/>
    public event EventHandler ViewReleaseRequested;

    /// <summary>처음 성공할 때 한 번 실행합니다. 성공적으로 닫은 화면을 다시 열면 재실행합니다.</summary>
    protected virtual Task OnInitialActivatedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>Active 상태와 메시지 구독을 설정한 뒤 실행합니다. 완료 후 Activated 이벤트를 보냅니다.</summary>
    protected virtual Task OnActivatedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>Deactivated 상태와 메시지 구독 해제를 반영한 뒤 정리합니다. 실패하면 재시도할 수 있습니다.</summary>
    protected virtual Task OnDeactivatedAsync() => Task.CompletedTask;
    /// <summary>View 해제와 Closed 상태를 반영한 뒤 정리합니다. 완료 후 Closed 이벤트를 보냅니다.</summary>
    protected virtual Task OnClosedAsync() => Task.CompletedTask;
    /// <summary>상태 확정 후 호출합니다. 여기서 같은 화면의 상태 전환을 기다리면 안 됩니다.</summary>
    protected virtual Task OnStateChangedAsync(ScreenState previousState, ScreenState newState) => Task.CompletedTask;
    /// <summary>View 로딩 알림입니다. 화면 활성화와 별개의 UI 이벤트입니다.</summary>
    protected virtual Task OnViewLoadedAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        long version = this.activationVersion;
        return this.lifecycle.RunAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (version != this.activationVersion)
                throw new OperationCanceledException(new CancellationToken(true));
            if (this.closePending)
                throw new InvalidOperationException("실패한 종료 정리를 CloseAsync로 완료한 뒤 다시 활성화해야 합니다.");
            if (this.deactivationPending)
                await this.DeactivateCoreAsync();
            if (this.IsActive)
                return;
            this.activationCancellation?.Dispose();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.activationCancellation = cancellation;
            bool initial = !this.initialized;
            ScreenState previous = this.ScreenState;
            try
            {
                await this.SetStateAsync(ScreenState.Active);
                if (initial)
                {
                    if (!this.longMessengerRegistered)
                    {
                        this.LongMessenger.RegisterAll(this);
                        this.longMessengerRegistered = true;
                    }
                    await this.OnInitialActivatedAsync(cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    this.initialized = true;
                }
                await this.OnActivatedAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
            }
            catch (Exception activationError)
            {
                try
                {
                    cancellation.Cancel();
                    await this.DeactivateCoreAsync();
                }
                catch (Exception cleanupError) { throw new AggregateException(activationError, cleanupError); }
                finally
                {
                    this.activationCancellation = null;
                    cancellation.Dispose();
                }
                throw;
            }
            await Execute.RunAsync(() => this.Activated?.Invoke(this, new ActivationEventArgs(previous, initial)));
            await this.NotifyStateChangedAsync(previous);
        });
    }

    /// <inheritdoc/>
    public Task DeactivateAsync()
    {
        this.CancelActivation();
        return this.lifecycle.RunAsync(this.DeactivateCoreAsync);
    }

    private async Task DeactivateCoreAsync()
    {
        if (this.ScreenState != ScreenState.Active && !this.deactivationPending)
            return;
        if (!this.deactivationPending)
            this.deactivationPreviousState = this.ScreenState;
        ScreenState previous = this.deactivationPreviousState;
        this.deactivationPending = true;
        await this.SetStateAsync(ScreenState.Deactivated);
        await this.OnDeactivatedAsync();
        this.deactivationPending = false;
        this.activationCancellation?.Dispose();
        this.activationCancellation = null;
        await Execute.RunAsync(() => this.Deactivated?.Invoke(this, new DeactivationEventArgs(previous)));
        await this.NotifyStateChangedAsync(previous);
    }

    /// <inheritdoc/>
    public Task CloseAsync()
    {
        this.CancelActivation();
        return this.lifecycle.RunAsync(async () =>
        {
            if (this.ScreenState == ScreenState.Closed && !this.closePending)
                return;
            await this.DeactivateCoreAsync();
            if (!this.closePending)
            {
                this.closePreviousState = this.ScreenState;
                this.ViewReleaseRequested?.Invoke(this, EventArgs.Empty);
                this.initialized = false;
                this.LongMessenger.UnregisterAll(this);
                this.longMessengerRegistered = false;
            }
            this.closePending = true;
            await this.SetStateAsync(ScreenState.Closed);
            await this.OnClosedAsync();
            this.closePending = false;
            ScreenState previous = this.closePreviousState;
            await Execute.RunAsync(() => this.Closed?.Invoke(this, new CloseEventArgs(previous)));
            await this.NotifyStateChangedAsync(previous);
        });
    }

    private void CancelActivation()
    {
        this.activationVersion++;
        this.activationCancellation?.Cancel();
    }

    private async Task SetStateAsync(ScreenState state)
    {
        ScreenState previous = this.screenState;
        this.screenState = state;
        // Toolkit의 동기 콜백은 메시지 구독만 연결하고, 비동기 훅은 전환 메서드가 직접 기다립니다.
        this.settingRecipientState = true;
        try { base.IsActive = state == ScreenState.Active; }
        finally { this.settingRecipientState = false; }
        if (previous == state) return;
        this.OnPropertyChanged(nameof(this.ScreenState));
        await this.OnStateChangedAsync(previous, state);
    }

    private Task NotifyStateChangedAsync(ScreenState previous) =>
        previous == this.ScreenState ? Task.CompletedTask : Execute.RunAsync(() =>
            this.StateChanged?.Invoke(this, new ScreenStateChangedEventArgs(this.ScreenState, previous)));

    /// <summary>Toolkit의 활성 메시지 수신 등록만 수행합니다. 사용자 훅은 OnActivatedAsync입니다.</summary>
    protected sealed override void OnActivated() => base.OnActivated();
    /// <summary>Toolkit의 메시지 수신 해제만 수행합니다. 사용자 훅은 OnDeactivatedAsync입니다.</summary>
    protected sealed override void OnDeactivated() => base.OnDeactivated();

    /// <summary>IsActive 직접 대입으로 비동기 수명주기를 우회하지 않도록 합니다.</summary>
    protected override void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(this.IsActive))
        {
            if (!this.settingRecipientState)
                throw new InvalidOperationException("IsActive 대신 ActivateAsync 또는 DeactivateAsync를 호출하세요.");
            // 외부 알림 콜백이 다시 IsActive를 쓰는 경우에도 전환을 우회하지 못하게 합니다.
            this.settingRecipientState = false;
        }
        base.OnPropertyChanging(e);
    }

    /// <inheritdoc/>
    Task IViewLifecycleAware.ViewLoadedAsync() => this.lifecycle.RunAsync(this.OnViewLoadedAsync);
    /// <inheritdoc/>
    public virtual Task<bool> CanCloseAsync() => Task.FromResult(true);
    /// <inheritdoc/>
    public virtual Task<bool> RequestCloseAsync(bool? dialogResult = null) =>
        this.Parent is IChildDelegate conductor
            ? conductor.CloseItemAsync(this, dialogResult)
            : Task.FromException<bool>(new InvalidOperationException("화면을 닫으려면 Conductor 또는 WindowManager에 연결되어 있어야 합니다."));
}
