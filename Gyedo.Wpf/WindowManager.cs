using Gyedo.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Data;

namespace Gyedo;

/// <summary>
/// Manager capable of taking a ViewModel instance, instantiating its View and showing it as a dialog or window
/// </summary>
public interface IWindowManager
{
    /// <summary>전체 창의 종료 허용 여부를 확인한 뒤 종료 완료를 기다립니다.</summary>
    Task<bool> CloseAllAsync();
    /// <summary>
    /// Given a ViewModel, show its corresponding View as a window
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    Task ShowWindowAsync(object viewModel);

    /// <summary>
    /// Given a ViewModel, show its corresponding View as a window, and set its owner
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    /// <param name="ownerViewModel">The ViewModel for the View which should own this window</param>
    Task ShowWindowAsync(object viewModel, object ownerViewModel);

    /// <summary>
    /// Given a ViewModel, show its corresponding View as a Dialog
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    /// <returns>DialogResult of the View</returns>
    Task<bool?> ShowDialogAsync(object viewModel);

    /// <summary>
    /// Given a ViewModel, show its corresponding View as a Dialog, and set its owner
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    /// <param name="ownerViewModel">The ViewModel for the View which should own this dialog</param>
    /// <returns>DialogResult of the View</returns>
    Task<bool?> ShowDialogAsync(object viewModel, object ownerViewModel);

    /// <summary>
    /// Display a MessageBox
    /// </summary>
    /// <param name="messageBoxText">A <see cref="string"/> that specifies the text to display.</param>
    /// <param name="caption">A <see cref="string"/> that specifies the title bar caption to display.</param>
    /// <param name="buttons">A <see cref="System.Windows.MessageBoxButton"/> value that specifies which button or buttons to display.</param>
    /// <param name="icon">A <see cref="System.Windows.MessageBoxImage"/> value that specifies the icon to display.</param>
    /// <param name="defaultResult">A <see cref="System.Windows.MessageBoxResult"/> value that specifies the default result of the message box.</param>
    /// <param name="cancelResult">A <see cref="System.Windows.MessageBoxResult"/> value that specifies the cancel result of the message box</param>
    /// <param name="buttonLabels">A dictionary specifying the button labels, if desirable</param>
    /// <param name="flowDirection">The <see cref="System.Windows.FlowDirection"/> to use, overrides the <see cref="MessageBoxViewModel.DefaultFlowDirection"/></param>
    /// <param name="textAlignment">The <see cref="System.Windows.TextAlignment"/> to use, overrides the <see cref="MessageBoxViewModel.DefaultTextAlignment"/></param>
    /// <returns>The result chosen by the user</returns>
    Task<MessageBoxResult> ShowMessageBoxAsync(string messageBoxText, string caption = "",
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        MessageBoxResult cancelResult = MessageBoxResult.None,
        IDictionary<MessageBoxResult, string> buttonLabels = null,
        FlowDirection? flowDirection = null,
        TextAlignment? textAlignment = null);
}

/// <summary>
/// Configuration passed to WindowManager (normally implemented by BootstrapperBase)
/// </summary>
public interface IWindowManagerConfig
{
    /// <summary>
    /// Returns the currently-displayed window, or null if there is none (or it can't be determined)
    /// </summary>
    /// <returns>The currently-displayed window, or null</returns>
    Window GetActiveWindow();
}

/// <summary>
/// Default implementation of IWindowManager, is capable of showing a ViewModel's View as a dialog or a window
/// </summary>
public class WindowManager : IWindowManager
{
    private static readonly ILogger logger = LogManager.GetLogger(typeof(WindowManager));
    private readonly List<WindowConductor> conductors = new();
    private readonly IViewManager viewManager;
    private readonly Func<IMessageBoxViewModel> messageBoxViewModelFactory;
    private readonly Func<Window> getActiveWindow;

    /// <summary>
    /// Initialises a new instance of the <see cref="WindowManager"/> class, using the given <see cref="IViewManager"/>
    /// </summary>
    /// <param name="viewManager">IViewManager to use when creating views</param>
    /// <param name="messageBoxViewModelFactory">Delegate which returns a new IMessageBoxViewModel instance when invoked</param>
    /// <param name="config">Configuration object</param>
    public WindowManager(IViewManager viewManager, Func<IMessageBoxViewModel> messageBoxViewModelFactory, IWindowManagerConfig config)
    {
        this.viewManager = viewManager;
        this.messageBoxViewModelFactory = messageBoxViewModelFactory;
        this.getActiveWindow = config.GetActiveWindow;
    }

    /// <summary>
    /// Given a ViewModel, show its corresponding View as a window
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    public async Task ShowWindowAsync(object viewModel)
    {
        await this.ShowWindowAsync(viewModel, null);
    }

    /// <summary>
    /// Given a ViewModel, show its corresponding View as a window, and set its owner
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    /// <param name="ownerViewModel">The ViewModel for the View which should own this window</param>
    public async Task ShowWindowAsync(object viewModel, object ownerViewModel)
    {
        (await this.CreateWindowAsync(viewModel, false, ownerViewModel)).Show();
    }

    /// <summary>
    /// Given a ViewModel, show its corresponding View as a Dialog
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    /// <returns>DialogResult of the View</returns>
    public async Task<bool?> ShowDialogAsync(object viewModel)
    {
        return await this.ShowDialogAsync(viewModel, null);
    }

    /// <summary>
    /// Given a ViewModel, show its corresponding View as a Dialog, and set its owner
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    /// <param name="ownerViewModel">The ViewModel for the View which should own this dialog</param>
    /// <returns>DialogResult of the View</returns>
    public async Task<bool?> ShowDialogAsync(object viewModel, object ownerViewModel)
    {
        Window window = await this.CreateWindowAsync(viewModel, true, ownerViewModel);
        return await window.Dispatcher.InvokeAsync(window.ShowDialog);
    }

    /// <summary>
    /// Display a MessageBox
    /// </summary>
    /// <param name="messageBoxText">A <see cref="string"/> that specifies the text to display.</param>
    /// <param name="caption">A <see cref="string"/> that specifies the title bar caption to display.</param>
    /// <param name="buttons">A <see cref="System.Windows.MessageBoxButton"/> value that specifies which button or buttons to display.</param>
    /// <param name="icon">A <see cref="System.Windows.MessageBoxImage"/> value that specifies the icon to display.</param>
    /// <param name="defaultResult">A <see cref="System.Windows.MessageBoxResult"/> value that specifies the default result of the message box.</param>
    /// <param name="cancelResult">A <see cref="System.Windows.MessageBoxResult"/> value that specifies the cancel result of the message box</param>
    /// <param name="buttonLabels">A dictionary specifying the button labels, if desirable</param>
    /// <param name="flowDirection">The <see cref="System.Windows.FlowDirection"/> to use, overrides the <see cref="MessageBoxViewModel.DefaultFlowDirection"/></param>
    /// <param name="textAlignment">The <see cref="System.Windows.TextAlignment"/> to use, overrides the <see cref="MessageBoxViewModel.DefaultTextAlignment"/></param>
    /// <returns>The result chosen by the user</returns>
    public async Task<MessageBoxResult> ShowMessageBoxAsync(string messageBoxText, string caption = "",
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        MessageBoxResult cancelResult = MessageBoxResult.None,
        IDictionary<MessageBoxResult, string> buttonLabels = null,
        FlowDirection? flowDirection = null,
        TextAlignment? textAlignment = null)
    {
        IMessageBoxViewModel vm = this.messageBoxViewModelFactory();
        vm.Setup(messageBoxText, caption, buttons, icon, defaultResult, cancelResult, buttonLabels, flowDirection, textAlignment);
        await this.ShowDialogAsync(vm);
        return vm.ClickedButton;
    }

    /// <summary>
    /// Given a ViewModel, create its View, ensure that it's a Window, and set it up
    /// </summary>
    /// <param name="viewModel">ViewModel to create the window for</param>
    /// <param name="isDialog">True if the window will be used as a dialog</param>
    /// <param name="ownerViewModel">Optionally the ViewModel which owns the view which should own this window</param>
    /// <returns>Window which was created and set up</returns>
    protected virtual async Task<Window> CreateWindowAsync(object viewModel, bool isDialog, object ownerViewModel)
    {
        UIElement view = this.viewManager.CreateAndBindViewForModelIfNecessary(viewModel);
        if (view is not Window window)
        {
            var e = new GyedoInvalidViewTypeException(string.Format("WindowManager.ShowWindow or .ShowDialog tried to show a View of type '{0}', but that View doesn't derive from the Window class. " +
                "Make sure any Views you display using WindowManager.ShowWindow or .ShowDialog derive from Window (not UserControl, etc)",
                view == null ? "(null)" : view.GetType().Name));
            logger.Error(e);
            throw e;
        }

        // Only set this it hasn't been set / bound to anything
        if (viewModel is IHaveDisplayName haveDisplayName && (string.IsNullOrEmpty(window.Title) || window.Title == view.GetType().Name) && BindingOperations.GetBindingBase(window, Window.TitleProperty) == null)
        {
            var binding = new Binding("DisplayName") { Mode = BindingMode.TwoWay };
            window.SetBinding(Window.TitleProperty, binding);
        }

        if (ownerViewModel != null && this.viewManager.GetView(ownerViewModel) is Window explicitOwner)
        {
            window.Owner = explicitOwner;
        }
        else if (isDialog)
        {
            Window owner = this.InferOwnerOf(window);
            if (owner != null)
            {
                // We can end up in a really weird situation if they try and display more than one dialog as the application's closing
                // Basically the MainWindow's no long active, so the second dialog chooses the first dialog as its owner... But the first dialog
                // hasn't yet been shown, so we get an exception ("cannot set owner property to a Window which has not been previously shown").
                try
                {
                    window.Owner = owner;
                }
                catch (InvalidOperationException e)
                {
                    logger.Error(e, "This can occur when the application is closing down");
                }
            }
        }

        if (isDialog)
        {
            logger.Info("Displaying ViewModel {0} with View {1} as a Dialog", viewModel, window);
        }
        else
        {
            logger.Info("Displaying ViewModel {0} with View {1} as a Window", viewModel, window);
        }

        // If and only if they haven't tried to position the window themselves...
        // Has to be done after we're attempted to set the owner
        if (window.WindowStartupLocation == WindowStartupLocation.Manual && double.IsNaN(window.Top) && double.IsNaN(window.Left) &&
            BindingOperations.GetBinding(window, Window.TopProperty) == null && BindingOperations.GetBinding(window, Window.LeftProperty) == null)
        {
            window.WindowStartupLocation = window.Owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        }

        // This gets itself retained by the window, by registering events
#pragma warning disable CA1806 // Do not ignore method results
        var conductor = new WindowConductor(this, window, viewModel, isDialog);
        this.conductors.Add(conductor);
        try { await ScreenExtensions.TryActivateAsync(viewModel); }
        catch
        {
            await conductor.AbortAsync();
            throw;
        }
#pragma warning restore CA1806 // Do not ignore method results

        return window;
    }

    /// <summary>모든 창의 종료 조건을 먼저 확인한 뒤 종료 완료를 기다립니다.</summary>
    public async Task<bool> CloseAllAsync()
    {
        WindowConductor[] snapshot = this.conductors.ToArray();
        foreach (WindowConductor conductor in snapshot)
            if (!await conductor.CanCloseAsync()) return false;
        foreach (WindowConductor conductor in snapshot)
            if (!await conductor.CloseAsync(null, checkGuard: false)) return false;
        return true;
    }

    private Window InferOwnerOf(Window window)
    {
        Window active = this.getActiveWindow();
        return ReferenceEquals(active, window) ? null : active;
    }

    private sealed class WindowConductor : IChildDelegate
    {
        private readonly WindowManager manager;
        private readonly Window window;
        private readonly object viewModel;
        private readonly bool isDialog;
        private Task<bool> closeTask;
        private bool allowClose;
        private bool closed;
        private bool lifecycleClosed;

        public WindowConductor(WindowManager manager, Window window, object viewModel, bool isDialog)
        {
            this.manager = manager;
            this.window = window;
            this.viewModel = viewModel;
            this.isDialog = isDialog;
            if (viewModel is IChild child) child.Parent = this;
            window.StateChanged += this.WindowStateChanged;
            window.Closing += this.WindowClosing;
            window.Closed += this.WindowClosed;
        }

        public Task<bool> CanCloseAsync() => this.viewModel is IGuardClose guard
            ? guard.CanCloseAsync() : Task.FromResult(true);

        private async void WindowStateChanged(object sender, EventArgs e)
        {
            try
            {
                if (this.window.WindowState == WindowState.Minimized)
                    await ScreenExtensions.TryDeactivateAsync(this.viewModel);
                else if (!this.closed && !this.allowClose)
                    await ScreenExtensions.TryActivateAsync(this.viewModel);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { logger.Error(exception, "Window state transition failed"); }
        }

        private async void WindowClosing(object sender, CancelEventArgs e)
        {
            if (this.allowClose || e.Cancel) return;
            e.Cancel = true;
            try { await this.CloseAsync(null); }
            catch (Exception exception) { logger.Error(exception, "Window close failed"); }
        }

        private async void WindowClosed(object sender, EventArgs e)
        {
            this.closed = true;
            this.Detach();
            // Application.Shutdown 등 Closing을 기다리지 않는 강제 종료 경로도 관찰합니다.
            try
            {
                if (!this.lifecycleClosed) await ScreenExtensions.TryCloseAsync(this.viewModel);
            }
            catch (Exception exception) { logger.Error(exception, "Forced window close failed"); }
            finally
            {
                if (this.viewModel is IChild child && ReferenceEquals(child.Parent, this)) child.Parent = null;
            }
        }

        public Task<bool> CloseItemAsync(object item, bool? dialogResult = null) =>
            ReferenceEquals(item, this.viewModel) ? this.CloseAsync(dialogResult) : Task.FromResult(false);

        public Task<bool> CloseAsync(bool? dialogResult, bool checkGuard = true)
        {
            if (this.closed) return Task.FromResult(true);
            if (this.closeTask != null && !this.closeTask.IsCompleted) return this.closeTask;
            return this.closeTask = this.CloseCoreAsync(dialogResult, checkGuard);
        }

        private async Task<bool> CloseCoreAsync(bool? dialogResult, bool checkGuard)
        {
            if (checkGuard && !await this.CanCloseAsync()) return false;
            await ScreenExtensions.TryCloseAsync(this.viewModel);
            this.lifecycleClosed = true;
            // Closing 이벤트 안에서 다시 Close하지 않도록 다음 Dispatcher 차례로 넘깁니다.
            await this.window.Dispatcher.InvokeAsync(() =>
            {
                if (this.closed) return;
                this.allowClose = true;
                try
                {
                    if (this.isDialog && dialogResult.HasValue) this.window.DialogResult = dialogResult;
                    else this.window.Close();
                }
                finally { this.allowClose = false; }
            });
            return this.closed;
        }

        public async Task AbortAsync()
        {
            this.Detach();
            try { await ScreenExtensions.TryCloseAsync(this.viewModel); }
            finally
            {
                if (this.viewModel is IChild child && ReferenceEquals(child.Parent, this)) child.Parent = null;
                this.window.Close();
                this.closed = true;
            }
        }

        private void Detach()
        {
            this.window.StateChanged -= this.WindowStateChanged;
            this.window.Closing -= this.WindowClosing;
            this.window.Closed -= this.WindowClosed;
            this.manager.conductors.Remove(this);
        }
    }
}
