using System;

namespace Gyedo.Samples.DesignMode;

public class JustIntellisenseViewModel : Screen
{
    public string TextBoxText { get; set; }

    public JustIntellisenseViewModel()
    {
        this.TextBoxText = "This text is not displayed in the View";
    }
}
