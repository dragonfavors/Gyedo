using System.Windows;

namespace Gyedo;

/// <summary>
/// Is aware of the fact that it has a view
/// </summary>
public interface IViewAware
{
    /// <summary>
    /// Gets the view associated with this ViewModel
    /// </summary>
    UIElement View { get; }

    /// <summary>
    /// Called when the view should be attached. Should set View property.
    /// </summary>
    /// <remarks>Separate from the View property so it can be explicitely implemented</remarks>
    /// <param name="view">View to attach</param>
    void AttachView(UIElement view);
}
