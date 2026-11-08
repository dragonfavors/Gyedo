using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Stylet.Samples.DesignMode;

public partial class UsingViewModelLocatorViewModel : Screen
{
    public string TextBoxText { get; set; }

    public UsingViewModelLocatorViewModel(IMessenger messenger) : base(null, messenger)
    {
    }

    public bool CanDoSomething => false;

    [RelayCommand(CanExecute = nameof(CanDoSomething))]
    public void DoSomething()
    {
    }
}
