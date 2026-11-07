using NUnit.Framework;
using Stylet;
using Stylet.Examples;

namespace StyletUnitTests;

[TestFixture]
public class ExampleCommandTests
{
    private class Model : PropertyChangedBase
    {
        private bool enabled;
        public bool Enabled
        {
            get => this.enabled;
            set => this.SetAndNotify(ref this.enabled, value);
        }
    }

    [Test]
    public void PropertyChangesRefreshTheGuardAndDisabledCommandsDoNotExecute()
    {
        var model = new Model();
        int executions = 0;
        int notifications = 0;
        var command = new DelegateCommand(model, () => executions++, () => model.Enabled);
        command.CanExecuteChanged += (sender, args) => notifications++;

        Assert.IsFalse(command.CanExecute(null));
        command.Execute(null);
        Assert.AreEqual(0, executions);

        model.Enabled = true;
        Assert.AreEqual(1, notifications);
        Assert.IsTrue(command.CanExecute(null));
        command.Execute(null);
        Assert.AreEqual(1, executions);

        model.Enabled = false;
        Assert.AreEqual(2, notifications);
        Assert.IsFalse(command.CanExecute(null));
    }

    [Test]
    public void ItemCommandsReceiveTheirBoundParameter()
    {
        object received = null;
        var item = new object();
        var command = new DelegateCommand(new Model(), parameter => received = parameter);

        command.Execute(item);

        Assert.AreSame(item, received);
    }
}
