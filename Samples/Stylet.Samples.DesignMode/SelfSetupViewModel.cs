using CommunityToolkit.Mvvm.Input;
using System;

namespace Stylet.Samples.DesignMode;

public partial class SelfSetupViewModel : Screen
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly IEventAggregator eventAggregator;
#pragma warning restore IDE0052 // Remove unread private members

    public string TextBoxText { get; set; }

    /// <summary>
    /// This constructor is called by the designer. Use it to set up dummy values
    /// </summary>
    public SelfSetupViewModel()
    {
        this.TextBoxText = "This is some dummy text.";
    }

    /// <summary>
    /// 디자이너에서는 호출하지 않으며, 런타임에 생성자 주입으로 사용합니다.
    /// </summary>
    public SelfSetupViewModel(IEventAggregator eventAggregator)
    {
        this.eventAggregator = eventAggregator;
    }

    public bool CanDoSomething => false;

    [RelayCommand(CanExecute = nameof(CanDoSomething))]
    public void DoSomething()
    {
    }
}
