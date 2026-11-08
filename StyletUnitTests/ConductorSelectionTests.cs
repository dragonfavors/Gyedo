using NUnit.Framework;
using Stylet;
using Stylet.Xaml;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;

namespace StyletUnitTests;

[TestFixture, Apartment(ApartmentState.STA)]
public class ConductorSelectionTests
{
    private SynchronizationContext previousContext;

    [SetUp]
    public void SetUp()
    {
        this.previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
    }

    [TearDown]
    public void TearDown() => SynchronizationContext.SetSynchronizationContext(this.previousContext);

    private sealed class DelayedScreen : Screen
    {
        public readonly TaskCompletionSource Finish = new();
        protected override Task OnActivatedAsync(CancellationToken cancellationToken) => this.Finish.Task.WaitAsync(cancellationToken);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SelectionAwaitsNavigationAndRestoresPreviousItemOnFailure(bool fail)
    {
        var conductor = new Conductor<Screen>.Collection.OneActive();
        var first = new Screen();
        var second = new DelayedScreen();
        var tabs = new TabControl { DataContext = conductor };
        tabs.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("Items"));
        tabs.SetBinding(Selector.SelectedItemProperty, new Binding("ActiveItem") { Mode = BindingMode.OneWay });
        ConductorSelection.SetIsEnabled(tabs, true);
        var window = new Window
        {
            Content = tabs, Width = 100, Height = 100, Left = -32000, Top = -32000,
            ShowActivated = false, ShowInTaskbar = false,
        };
        try
        {
            await conductor.AddItemAsync(first);
            await conductor.AddItemAsync(second);
            await conductor.ActivateItemAsync(first);
            await conductor.ActivateAsync();
            window.Show();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            tabs.SetCurrentValue(Selector.SelectedItemProperty, second);
            Assert.That(tabs.IsEnabled, Is.False);
            Assert.That(conductor.ActiveItem, Is.SameAs(first));
            if (fail) second.Finish.SetException(new InvalidOperationException("load failed"));
            else second.Finish.SetResult();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.That(tabs.IsEnabled, Is.True);
            Assert.That(tabs.SelectedItem, Is.SameAs(fail ? first : second));
            Assert.That(conductor.ActiveItem, Is.SameAs(fail ? first : second));
        }
        finally
        {
            second.Finish.TrySetCanceled();
            window.Close();
            await conductor.CloseAsync();
        }
    }
}
