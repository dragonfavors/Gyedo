using System;

using CommunityToolkit.Mvvm.Messaging;

namespace Gyedo.Samples.DesignMode;

public class ViewModelLocator
{
    public UsingViewModelLocatorViewModel UsingViewModelLocatorViewModel
    {
        get
        {
            var vm = new UsingViewModelLocatorViewModel(new WeakReferenceMessenger());
            vm.TextBoxText = "This is some dummy text.";
            return vm;
        }
    }
}
