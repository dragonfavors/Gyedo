using System;
using System.Windows;
using System.Windows.Threading;

namespace Stylet;

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
    public void Post(Action action)
    {
        this.dispatcher.BeginInvoke(action);
    }

    /// <inheritdoc/>
    public void Send(Action action)
    {
        this.dispatcher.Invoke(action);
    }

    /// <inheritdoc/>
    public bool IsCurrent => this.dispatcher.CheckAccess();
}
