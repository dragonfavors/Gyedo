using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace Stylet;

/// <summary>
/// Extension of PropertyChangedEventArgs, which includes the new value of the property
/// </summary>
/// <typeparam name="TProperty">Type of property being notified</typeparam>
public class PropertyChangedExtendedEventArgs<TProperty> : PropertyChangedEventArgs
{
    /// <summary>
    /// Gets the new value of the property
    /// </summary>
    public virtual TProperty NewValue { get; private set; }

    /// <summary>
    /// Initialises a new instance of the <see cref="PropertyChangedExtendedEventArgs{TProperty}"/> class
    /// </summary>
    /// <param name="propertyName">Name of the property which changed</param>
    /// <param name="newValue">New value of the property which changed</param>
    public PropertyChangedExtendedEventArgs(string propertyName, TProperty newValue)
        : base(propertyName)
    {
        this.NewValue = newValue;
    }
}

/// <summary>
/// A binding to a PropertyChanged event, which can be used to unbind the binding
/// </summary>
public interface IEventBinding
{
    /// <summary>
    /// Unbind this event binding, so that it will no longer receive events
    /// </summary>
    void Unbind();
}

/// <summary>
/// 속성 변경 이벤트의 구독과 해제를 지원합니다.
/// </summary>
public static class PropertyChangedExtensions
{
    internal class StrongPropertyChangedBinding : IEventBinding
    {
        private readonly WeakReference<INotifyPropertyChanged> inpc;
        private readonly PropertyChangedEventHandler handler;

        public StrongPropertyChangedBinding(INotifyPropertyChanged inpc, PropertyChangedEventHandler handler)
        {
            this.inpc = new WeakReference<INotifyPropertyChanged>(inpc);
            this.handler = handler;
        }

        public void Unbind()
        {
            if (this.inpc.TryGetTarget(out INotifyPropertyChanged inpc))
                inpc.PropertyChanged -= this.handler;
        }
    }

    /// <summary>
    /// Strongly bind to PropertyChanged events for a particular property on a particular object
    /// </summary>
    /// <example>someObject.Bind(x => x.PropertyNameToBindTo, newValue => /* do something with the new value */)</example>
    /// <typeparam name="TSource">Type of object providing the PropertyChanged event</typeparam>
    /// <typeparam name="TProperty">Type of property for which the event is raised</typeparam>
    /// <param name="target">Object raising the PropertyChanged event you're interested in</param>
    /// <param name="targetSelector">MemberExpression selecting the property to observe for changes (e.g x => x.PropertyName)</param>
    /// <param name="handler">Handler called whenever that property changed</param>
    /// <returns>Something which can be used to undo the binding. You can discard it if you want</returns>
    public static IEventBinding Bind<TSource, TProperty>(this TSource target, Expression<Func<TSource, TProperty>> targetSelector, EventHandler<PropertyChangedExtendedEventArgs<TProperty>> handler) where TSource : class, INotifyPropertyChanged
    {
        return BindImpl(target, targetSelector, handler, invoke: false);
    }

    /// <summary>
    /// Strongly bind to PropertyChanged events for a particular property on a particular object,
    /// and invoke the handler straight away
    /// </summary>
    /// <remarks>
    /// This immediately calls <paramref name="handler"/> with the current value of the property.
    /// </remarks>
    /// <example>someObject.BindAndInvoke(x => x.PropertyNameToBindTo, newValue => /* do something with the new value */)</example>
    /// <typeparam name="TSource">Type of object providing the PropertyChanged event</typeparam>
    /// <typeparam name="TProperty">Type of property for which the event is raised</typeparam>
    /// <param name="target">Object raising the PropertyChanged event you're interested in</param>
    /// <param name="targetSelector">MemberExpression selecting the property to observe for changes (e.g x => x.PropertyName)</param>
    /// <param name="handler">Handler called whenever that property changed</param>
    /// <returns>Something which can be used to undo the binding. You can discard it if you want</returns>
    public static IEventBinding BindAndInvoke<TSource, TProperty>(this TSource target, Expression<Func<TSource, TProperty>> targetSelector, EventHandler<PropertyChangedExtendedEventArgs<TProperty>> handler) where TSource : class, INotifyPropertyChanged
    {
        return BindImpl(target, targetSelector, handler, invoke: true);
    }

    private static IEventBinding BindImpl<TSource, TProperty>(TSource target, Expression<Func<TSource, TProperty>> targetSelector, EventHandler<PropertyChangedExtendedEventArgs<TProperty>> handler, bool invoke) where TSource : class, INotifyPropertyChanged
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (targetSelector == null)
            throw new ArgumentNullException(nameof(targetSelector));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        string propertyName = targetSelector.NameForProperty();
        Func<TSource, TProperty> propertyAccess = targetSelector.Compile();
        // Make sure we don't capture target strongly, otherwise we'll retain it when we shouldn't
        // If it does get released, we're released from the delegate list
        var weakTarget = new WeakReference<TSource>(target);

        void OurHandler(object o, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == propertyName || e.PropertyName == string.Empty)
            {
                if (weakTarget.TryGetTarget(out TSource strongTarget))
                    handler(strongTarget, new PropertyChangedExtendedEventArgs<TProperty>(propertyName, propertyAccess(strongTarget)));
            }
        }

        target.PropertyChanged += OurHandler;

        var listener = new StrongPropertyChangedBinding(target, OurHandler);

        if (invoke)
        {
            handler(target, new PropertyChangedExtendedEventArgs<TProperty>(propertyName, propertyAccess(target)));
        }

        return listener;
    }
}
