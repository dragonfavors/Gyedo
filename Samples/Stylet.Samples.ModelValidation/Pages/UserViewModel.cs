using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using System;
using System.Collections.Generic;

namespace Stylet.Samples.ModelValidation.Pages;

public partial class UserViewModel : Screen
{
    private readonly IWindowManager windowManager;

    [ObservableProperty]
    private string userName;
    [ObservableProperty]
    private string email;
    [ObservableProperty]
    private string password;
    [ObservableProperty]
    private string passwordConfirmation;
    [ObservableProperty]
    private Stringable<int> age;

    public bool ShouldAutoValidate
    {
        get => base.AutoValidate;
        set
        {
            if (base.AutoValidate == value)
                return;
            base.AutoValidate = value;
            this.OnPropertyChanged();
            this.OnPropertyChanged(nameof(this.CanSubmit));
            Execute.Run(this.SubmitCommand.NotifyCanExecuteChanged);
        }
    }

    public UserViewModel(IWindowManager windowManager, IModelValidator<UserViewModel> validator) : base(validator)
    {
        this.windowManager = windowManager;
        // Force initial validation
        this.Validate();
        // Whenever password changes, we need to re-validate PasswordConfirmation
        this.Bind(x => x.Password, (o, e) => this.ValidateProperty(() => this.PasswordConfirmation));
    }

    protected override void OnValidationStateChanged(IEnumerable<string> changedProperties)
    {
        base.OnValidationStateChanged(changedProperties);
        this.OnPropertyChanged(nameof(this.CanSubmit));
        Execute.Run(this.SubmitCommand.NotifyCanExecuteChanged);
    }

    public bool CanSubmit => !this.ShouldAutoValidate || !this.HasErrors;
    [RelayCommand(CanExecute = nameof(CanSubmit))]
    public async Task Submit()
    {
        if (this.Validate())
            await this.windowManager.ShowMessageBoxAsync("Successfully submitted", "success");
    }
}

public class UserViewModelValidator : AbstractValidator<UserViewModel>
{
    public UserViewModelValidator()
    {
        this.RuleFor(x => x.UserName).NotEmpty().Length(1, 20);
        this.RuleFor(x => x.Email).NotEmpty().EmailAddress();
        this.RuleFor(x => x.Password).NotEmpty().Matches("[0-9]").WithMessage("{PropertyName} must contain a number");
        this.RuleFor(x => x.PasswordConfirmation).Equal(s => s.Password).WithMessage("{PropertyName} should match Password");
        this.RuleFor(x => x.Age).Must(x => x.IsValid).WithMessage("{PropertyName} must be a valid number");
        this.When(x => x.Age.IsValid, () => this.RuleFor(x => x.Age.Value).GreaterThan(0).WithName("Age"));
    }
}
