using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Stylet;

// WPF 메시지 루프가 호스트 수명을 소유합니다. ConsoleLifetime은 사용하지 않습니다.
internal sealed class WpfHostLifetime : IHostLifetime, IDisposable
{
    private static readonly ConditionalWeakTable<Application, WpfHostLifetime> lifetimes = new();
    private readonly Application application;
    private readonly Func<Task> shutdown;
    private ShutdownMode shutdownMode;
    private bool attached;
    private readonly HashSet<Window> windows = new();
    private Window closingMainWindow;
    private int disposed;

    static WpfHostLifetime()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    public WpfHostLifetime(Application application, Func<Task> shutdown, bool attach = true)
    {
        this.application = application;
        this.shutdown = shutdown;
        if (attach)
            this.Attach();
    }

    public void Attach()
    {
        if (this.application == null || this.attached)
            return;
        this.application.Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(this.disposed != 0, this);
        this.shutdownMode = this.application.ShutdownMode;
        lifetimes.Add(this.application, this);
        this.attached = true;
        this.application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        foreach (Window window in this.application.Windows)
            this.Track(window);
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, e.OriginalSource) && Application.Current is Application app &&
            lifetimes.TryGetValue(app, out WpfHostLifetime lifetime))
            lifetime.Track((Window)sender);
    }

    private void Track(Window window)
    {
        if (this.disposed == 0 && this.windows.Add(window))
        {
            window.Closing += this.OnWindowClosing;
            window.Closed += this.OnWindowClosed;
        }
    }

    private void OnWindowClosing(object sender, CancelEventArgs e)
    {
        // Closed 시점에는 Application.MainWindow가 이미 비워질 수 있습니다.
        if (!e.Cancel && ReferenceEquals(sender, this.application.MainWindow))
            this.closingMainWindow = (Window)sender;
        else if (ReferenceEquals(sender, this.closingMainWindow))
            this.closingMainWindow = null;
    }

    private void OnWindowClosed(object sender, EventArgs e)
    {
        var window = (Window)sender;
        window.Closing -= this.OnWindowClosing;
        window.Closed -= this.OnWindowClosed;
        this.windows.Remove(window);
        bool mainWindowClosed = ReferenceEquals(window, this.closingMainWindow);
        if (mainWindowClosed)
            this.closingMainWindow = null;
        this.application.Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (this.disposed != 0 || this.shutdownMode == ShutdownMode.OnExplicitShutdown)
                return;
            if ((this.shutdownMode == ShutdownMode.OnMainWindowClose && mainWindowClosed) ||
                (this.shutdownMode == ShutdownMode.OnLastWindowClose && this.application.Windows.Count == 0))
            {
                try { await this.shutdown(); }
                catch (Exception exception) { Trace.TraceError("호스트 종료 실패: {0}", exception); }
            }
        }));
    }

    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0 || !this.attached)
            return;
        lifetimes.Remove(this.application);
        if (this.application.Dispatcher.CheckAccess())
            this.Detach();
        else
            this.application.Dispatcher.BeginInvoke(new Action(this.Detach));
    }

    private void Detach()
    {
        foreach (Window window in this.windows)
        {
            window.Closing -= this.OnWindowClosing;
            window.Closed -= this.OnWindowClosed;
        }
        this.windows.Clear();
        this.closingMainWindow = null;
        this.application.ShutdownMode = this.shutdownMode;
    }
}
