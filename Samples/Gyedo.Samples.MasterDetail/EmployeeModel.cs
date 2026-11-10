using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Gyedo.Samples.MasterDetail;

public class EmployeeModel : ObservableObject
{
    private string _name;
    public string Name
    {
        get => this._name;
        set => this.SetProperty(ref this._name, value);
    }
}
