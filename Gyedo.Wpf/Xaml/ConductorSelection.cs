using Gyedo.Logging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Gyedo.Xaml;

/// <summary>Selector의 선택 변경을 Conductor의 비동기 탐색에 연결합니다.</summary>
public static class ConductorSelection
{
    /// <summary>비동기 Conductor 선택 연결 여부입니다.</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ConductorSelection), new PropertyMetadata(false, OnEnabledChanged));
    private static readonly DependencyProperty IsChangingProperty = DependencyProperty.RegisterAttached(
        "IsChanging", typeof(bool), typeof(ConductorSelection), new PropertyMetadata(false));

    /// <summary>연결 여부를 반환합니다.</summary>
    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    /// <summary>연결 여부를 설정합니다.</summary>
    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not Selector selector) return;
        if ((bool)e.NewValue) selector.SelectionChanged += OnSelectionChanged;
        else selector.SelectionChanged -= OnSelectionChanged;
    }

    private static async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selector = (Selector)sender;
        if (!ReferenceEquals(e.OriginalSource, selector) || !selector.IsLoaded ||
            (bool)selector.GetValue(IsChangingProperty) || selector.DataContext is not IActiveItemConductor conductor ||
            ReferenceEquals(selector.SelectedItem, conductor.ActiveItem)) return;

        selector.SetValue(IsChangingProperty, true);
        bool enabled = selector.IsEnabled;
        selector.SetCurrentValue(UIElement.IsEnabledProperty, false);
        try { await conductor.ActivateItemAsync(selector.SelectedItem); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { LogManager.GetLogger(typeof(ConductorSelection)).Error(exception, "Tab navigation failed"); }
        finally
        {
            selector.SetCurrentValue(Selector.SelectedItemProperty, conductor.ActiveItem);
            selector.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateTarget();
            selector.SetCurrentValue(UIElement.IsEnabledProperty, enabled);
            selector.SetValue(IsChangingProperty, false);
        }
    }
}
