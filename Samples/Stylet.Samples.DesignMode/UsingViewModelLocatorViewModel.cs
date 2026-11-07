using Stylet.Examples;
using System.Windows.Input;
using System;

namespace Stylet.Samples.DesignMode;

public class UsingViewModelLocatorViewModel : Screen
{
    private DelegateCommand doSomethingCommand;
    public ICommand DoSomethingCommand => this.doSomethingCommand ?? (this.doSomethingCommand = new DelegateCommand(this, this.DoSomething, () => this.CanDoSomething));

#pragma warning disable IDE0052 // Remove unread private members
    private readonly IEventAggregator eventAggregator;
#pragma warning restore IDE0052 // Remove unread private members

    public string TextBoxText { get; set; }

    public UsingViewModelLocatorViewModel(IEventAggregator eventAggregator)
    {
        this.eventAggregator = eventAggregator;
    }

    public bool CanDoSomething => false;

    public void DoSomething()
    {
    }
}
