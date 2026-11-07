using Stylet.Examples;
using System.Windows.Input;
using System;
using System.Linq;

namespace Stylet.Samples.MasterDetail;

public class ShellViewModel : Screen
{
    private DelegateCommand addEmployeeCommand;
    public ICommand AddEmployeeCommand => this.addEmployeeCommand ?? (this.addEmployeeCommand = new DelegateCommand(this, this.AddEmployee));

    private DelegateCommand removeEmployeeCommand;
    public ICommand RemoveEmployeeCommand => this.removeEmployeeCommand ?? (this.removeEmployeeCommand = new DelegateCommand(this, parameter => this.RemoveEmployee((EmployeeModel)parameter)));

    public IObservableCollection<EmployeeModel> Employees { get; private set; }

    private EmployeeModel _selectedEmployee;
    public EmployeeModel SelectedEmployee
    {
        get => this._selectedEmployee;
        set => this.SetProperty(ref this._selectedEmployee, value);
    }

    public ShellViewModel()
    {
        this.DisplayName = "Master-Detail";

        this.Employees = new BindableCollection<EmployeeModel>
        {
            new EmployeeModel() { Name = "Fred" },
            new EmployeeModel() { Name = "Bob" }
        };

        this.SelectedEmployee = this.Employees.FirstOrDefault();
    }

    public void AddEmployee()
    {
        this.Employees.Add(new EmployeeModel() { Name = "Unnamed" });
    }

    public void RemoveEmployee(EmployeeModel item)
    {
        this.Employees.Remove(item);
    }
}
