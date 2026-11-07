using Stylet.Logging;
using System;
using System.Threading.Tasks;

namespace Stylet;

/// <summary>
/// Implementation of IScreen. Useful as a base class for your ViewModels
/// </summary>
public class Screen : ValidatingModelBase, IScreen, IViewLifecycleAware
{
    private readonly ILogger logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="Screen"/> class, without setting up a validator
    /// </summary>
    public Screen() : this(null) { }

    /// <summary>
    /// Initialises a new instance of the <see cref="Screen"/> class, which can validate properties using the given validator
    /// </summary>
    /// <param name="validator">Validator to use</param>
    public Screen(IModelValidator validator) : base(validator)
    {
        Type type = this.GetType();
        this.DisplayName = type.FullName;
        this.logger = LogManager.GetLogger(type);
    }

    #region IHaveDisplayName

    private string _displayName;

    /// <summary>
    /// Gets or sets the name associated with this ViewModel.
    /// Shown e.g. in a window's title bar, or as a tab's displayName
    /// </summary>
    public string DisplayName
    {
        get => this._displayName;
        set => this.SetProperty(ref this._displayName, value);
    }

    #endregion

    #region IScreenState

    private ScreenState _screenState = ScreenState.Deactivated;

    /// <summary>
    /// Gets or sets the current state of the Screen
    /// </summary>
    public virtual ScreenState ScreenState
    {
        get => this._screenState;
        protected set
        {
            this.SetProperty(ref this._screenState, value);
            this.OnPropertyChanged(nameof(this.IsActive));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current state is ScreenState.Active
    /// </summary>
    public bool IsActive => this.ScreenState == ScreenState.Active;

    private bool haveActivated = false;

    /// <summary>
    /// Raised when the Screen's state changed, for any reason
    /// </summary>
    public event EventHandler<ScreenStateChangedEventArgs> StateChanged;

    /// <summary>
    /// Fired whenever the Screen is activated
    /// </summary>
    public event EventHandler<ActivationEventArgs> Activated;

    /// <summary>
    /// Fired whenever the Screen is deactivated
    /// </summary>
    public event EventHandler<DeactivationEventArgs> Deactivated;

    /// <summary>
    /// Called whenever this Screen is closed
    /// </summary>
    public event EventHandler<CloseEventArgs> Closed;

    /// <inheritdoc/>
    public event EventHandler ViewReleaseRequested;

    /// <summary>
    /// Called the very first time this Screen is activated, and never again
    /// </summary>
    protected virtual void OnInitialActivate() { }

    /// <summary>
    /// Called every time this screen is activated
    /// </summary>
    protected virtual void OnActivate() { }

    /// <summary>
    /// Called every time this screen is deactivated
    /// </summary>
    protected virtual void OnDeactivate() { }

    /// <summary>
    /// Called when this screen is closed
    /// </summary>
    protected virtual void OnClose() { }

    /// <summary>
    /// Called on any state transition
    /// </summary>
    /// <param name="previousState">Previous state state</param>
    /// <param name="newState">New state</param>
    protected virtual void OnStateChanged(ScreenState previousState, ScreenState newState) { }

    /// <summary>
    /// Sets the screen's state to the given state, if it differs from the current state
    /// </summary>
    /// <param name="newState">State to transition to</param>
    /// <param name="changedHandler">Called if the transition occurs. Arguments are (newState, previousState)</param>
    protected virtual void SetState(ScreenState newState, Action<ScreenState, ScreenState> changedHandler)
    {
        if (newState == this.ScreenState)
            return;

        ScreenState previousState = this.ScreenState;
        this.ScreenState = newState;

        this.logger.Info("Setting state from {0} to {1}", previousState, newState);

        this.OnStateChanged(previousState, newState);
        changedHandler(previousState, newState);

        EventHandler<ScreenStateChangedEventArgs> handler = this.StateChanged;
        if (handler != null)
            Execute.OnUIThread(() => handler(this, new ScreenStateChangedEventArgs(newState, previousState)));
    }

    void IScreenState.Activate()
    {
        this.SetState(ScreenState.Active, (oldState, newState) =>
        {
            bool isInitialActivate = !this.haveActivated;
            if (!this.haveActivated)
            {
                this.OnInitialActivate();
                this.haveActivated = true;
            }

            this.OnActivate();

            EventHandler<ActivationEventArgs> handler = this.Activated;
            if (handler != null)
                Execute.OnUIThread(() => handler(this, new ActivationEventArgs(oldState, isInitialActivate)));
        });
    }

    void IScreenState.Deactivate()
    {
        // Avoid going from Closed -> Deactivated without going via Activated
        if (this.ScreenState == ScreenState.Closed)
            ((IScreenState)this).Activate();

        this.SetState(ScreenState.Deactivated, (oldState, newState) =>
        {
            this.OnDeactivate();

            EventHandler<DeactivationEventArgs> handler = this.Deactivated;
            if (handler != null)
                Execute.OnUIThread(() => handler(this, new DeactivationEventArgs(oldState)));
        });
    }

    void IScreenState.Close()
    {
        // Avoid going from Activated -> Closed without going via Deactivated
        if (this.ScreenState != ScreenState.Closed)
            ((IScreenState)this).Deactivate();

        this.ViewReleaseRequested?.Invoke(this, EventArgs.Empty);

        // Reset, so we can initially activate again
        this.haveActivated = false;

        this.SetState(ScreenState.Closed, (oldState, newState) =>
        {
            this.OnClose();

            EventHandler<CloseEventArgs> handler = this.Closed;
            if (handler != null)
                Execute.OnUIThread(() => handler(this, new CloseEventArgs(oldState)));
        });
    }

    #endregion

    /// <summary>
    /// UI 어댑터가 View 로딩을 알릴 때 호출됩니다. 화면 활성화와는 별개입니다.
    /// </summary>
    void IViewLifecycleAware.ViewLoaded() => this.OnViewLoaded();

    /// <summary>
    /// 연결된 View가 로드되었을 때 실행할 작업입니다.
    /// </summary>
    protected virtual void OnViewLoaded() { }

    #region IChild

    private object _parent;

    /// <summary>
    /// Gets or sets the parent conductor of this screen. Used to RequestClose to request a closure
    /// </summary>
    public object Parent
    {
        get => this._parent;
        set => this.SetProperty(ref this._parent, value);
    }

    #endregion

    #region IGuardClose

    /// <summary>
    /// Called when a conductor wants to know whether this screen can close.
    /// </summary>
    /// <returns>A task returning true (can close) or false (can't close)</returns>
    public virtual Task<bool> CanCloseAsync()
    {
        return Task.FromResult(true);
    }

    #endregion

    #region IRequestClose

    /// <summary>
    /// Request that the conductor responsible for this screen close it
    /// </summary>
    /// <param name="dialogResult">DialogResult to return, if this is a dialog</param>
    public virtual void RequestClose(bool? dialogResult = null)
    {
        if (this.Parent is IChildDelegate conductor)
        {
            this.logger.Info("RequstClose called. Conductor: {0}; DialogResult: {1}", conductor, dialogResult);
            conductor.CloseItem(this, dialogResult);
        }
        else
        {
            var e = new InvalidOperationException(string.Format("Unable to close ViewModel {0} as it must have a conductor as a parent (note that windows and dialogs automatically have such a parent)", this.GetType()));
            this.logger.Error(e);
            throw e;
        }
    }

    #endregion
}
