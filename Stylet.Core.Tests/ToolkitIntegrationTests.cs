using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moq;
using NUnit.Framework;
using Stylet;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StyletUnitTests;

[TestFixture]
public partial class ToolkitIntegrationTests
{
    private partial class GeneratedScreen : Screen
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Heading))]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string name;

        public string Heading => $"Hello, {this.Name}";
        public int SaveCount { get; private set; }

        public GeneratedScreen(IModelValidator validator = null) : base(validator) { }

        public void DisableAutoValidation() => this.AutoValidate = false;
        public void NotifyAll() => this.OnPropertyChanged(string.Empty);

        private bool CanSave() => !string.IsNullOrWhiteSpace(this.Name);

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save() => this.SaveCount++;
    }

    private partial class GeneratedConductor : Conductor<Screen>
    {
        [ObservableProperty]
        private string status;
    }

    private static Mock<IModelValidator> CreateValidator()
    {
        var validator = new Mock<IModelValidator>();
        validator.Setup(x => x.ValidatePropertyAsync(It.IsAny<string>())).ReturnsAsync((IEnumerable<string>)null);
        return validator;
    }

    [Test]
    public void GeneratedScreenNotifiesBeforeAndAfterChangingAProperty()
    {
        var screen = new GeneratedScreen();
        var changed = new List<string>();
        string valueBeforeChange = "unset";
        screen.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName == nameof(GeneratedScreen.Name))
                valueBeforeChange = screen.Name;
        };
        screen.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        screen.Name = "Ada";
        screen.Name = "Ada";

        Assert.IsNull(valueBeforeChange);
        Assert.AreEqual("Ada", screen.Name);
        CollectionAssert.AreEqual(new[] { "Name", "Heading" }, changed);
    }

    [Test]
    public void GeneratedPropertiesRefreshCommandGuards()
    {
        var screen = new GeneratedScreen();
        int notifications = 0;
        screen.SaveCommand.CanExecuteChanged += (_, _) => notifications++;
        Assert.IsFalse(screen.SaveCommand.CanExecute(null));

        screen.Name = "Ada";
        Assert.IsTrue(screen.SaveCommand.CanExecute(null));
        screen.SaveCommand.Execute(null);
        Assert.AreEqual(1, screen.SaveCount);

        screen.Name = string.Empty;
        Assert.IsFalse(screen.SaveCommand.CanExecute(null));
        Assert.AreEqual(2, notifications);
    }

    [Test]
    public void GeneratedPropertiesWorkOnConductors()
    {
        var conductor = new GeneratedConductor();
        string changed = null;
        conductor.PropertyChanged += (_, e) => changed = e.PropertyName;

        conductor.Status = "Ready";
        Assert.AreEqual(nameof(GeneratedConductor.Status), changed);

        var child = new Screen();
        ((IScreenState)conductor).Activate();
        conductor.ActivateItem(child);
        Assert.AreSame(child, conductor.ActiveItem);
        Assert.IsTrue(child.IsActive);
    }

    [Test]
    public void GeneratedPropertiesUseTheExistingValidatorWithoutRecursingOnHasErrors()
    {
        var validator = CreateValidator();
        validator.Setup(x => x.ValidatePropertyAsync("Name")).ReturnsAsync(new[] { "Invalid name" });
        var screen = new GeneratedScreen(validator.Object);

        screen.Name = "Ada";

        Assert.IsTrue(screen.HasErrors);
        CollectionAssert.AreEqual(new[] { "Invalid name" }, screen.GetErrors("Name"));
        validator.Verify(x => x.ValidatePropertyAsync("Name"), Times.Once);
        validator.Verify(x => x.ValidatePropertyAsync(nameof(Screen.HasErrors)), Times.Never);
    }

    [Test]
    public void GeneratedPropertiesRespectAutoValidate()
    {
        var validator = CreateValidator();
        var screen = new GeneratedScreen(validator.Object);
        screen.DisableAutoValidation();
        validator.Invocations.Clear();

        screen.Name = "Ada";

        validator.Verify(x => x.ValidatePropertyAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void PropertyAndValidationNotificationsUseTheConfiguredDispatcher()
    {
        var validator = CreateValidator();
        validator.Setup(x => x.ValidatePropertyAsync("Name")).ReturnsAsync(new[] { "Invalid name" });
        var screen = new GeneratedScreen(validator.Object);
        var pending = new List<Action>();
        var properties = new List<string>();
        var errors = new List<string>();
        screen.PropertyChangedDispatcher = pending.Add;
        screen.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        screen.ErrorsChanged += (_, e) => errors.Add(e.PropertyName);

        screen.Name = "Ada";
        Assert.IsTrue(screen.HasErrors);
        Assert.IsEmpty(properties);
        Assert.IsEmpty(errors);

        foreach (Action callback in pending)
            callback();

        CollectionAssert.AreEquivalent(new[] { "Name", "Heading", "HasErrors" }, properties);
        CollectionAssert.AreEqual(new[] { "Name" }, errors);
    }

    [Test]
    public void ModelsCaptureTheDefaultDispatcherAtConstruction()
    {
        Action<Action> previous = Execute.DefaultPropertyChangedDispatcher;
        try
        {
            var pending = new List<Action>();
            Execute.DefaultPropertyChangedDispatcher = pending.Add;
            var screen = new GeneratedScreen();
            pending.Clear();
            Execute.DefaultPropertyChangedDispatcher = action => action();
            int notifications = 0;
            screen.PropertyChanged += (_, _) => notifications++;

            screen.Name = "Ada";
            Assert.AreEqual(0, notifications);
            foreach (Action callback in pending)
                callback();
            Assert.AreEqual(2, notifications);
        }
        finally
        {
            Execute.DefaultPropertyChangedDispatcher = previous;
        }
    }

    [Test]
    public async Task GeneratedPropertiesReportAsynchronousValidationResults()
    {
        var validator = CreateValidator();
        var validation = new TaskCompletionSource<IEnumerable<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        validator.Setup(x => x.ValidatePropertyAsync("Name")).Returns(validation.Task);
        var screen = new GeneratedScreen(validator.Object);
        var notified = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        screen.ErrorsChanged += (_, e) =>
        {
            if (e.PropertyName == "Name")
                notified.TrySetResult(true);
        };

        screen.Name = "Ada";
        Assert.IsFalse(screen.HasErrors);
        validation.SetResult(new[] { "Remote validation failed" });
        await notified.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(screen.HasErrors);
        CollectionAssert.AreEqual(new[] { "Remote validation failed" }, screen.GetErrors("Name"));
        validator.Verify(x => x.ValidatePropertyAsync("Name"), Times.Once);
    }

    [Test]
    public void ExplicitAllPropertiesNotificationStillReachesTheValidator()
    {
        var validator = CreateValidator();
        var screen = new GeneratedScreen(validator.Object);
        string notified = null;
        screen.PropertyChanged += (_, e) => notified = e.PropertyName;

        screen.NotifyAll();

        Assert.AreEqual(string.Empty, notified);
        validator.Verify(x => x.ValidatePropertyAsync(string.Empty), Times.Once);
    }
}
