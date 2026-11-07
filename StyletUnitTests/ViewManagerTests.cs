using Moq;
using NUnit.Framework;
using Stylet;
using Stylet.Xaml;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StyletUnitTests;

public class ViewManagerTestsViewModel
{
}

public class ViewManagerTestsView
{
}

[TestFixture, Apartment(ApartmentState.STA)]
public class ViewManagerTests
{
    private interface I1 { }
    private abstract class AC1 { }
    private class C1 { }

    private class AccessibleViewManager : ViewManager
    {
        public AccessibleViewManager(ViewManagerConfig config)
            : base(config) { }

        public new UIElement CreateViewForModel(object model)
        {
            return base.CreateViewForModel(model);
        }

        public new void BindViewToModel(UIElement view, object viewModel)
        {
            base.BindViewToModel(view, viewModel);
        }

        public new string ViewTypeNameForModelTypeName(string modelTypeName)
        {
            return base.ViewTypeNameForModelTypeName(modelTypeName);
        }

        public new Type LocateViewForModel(Type modelType)
        {
            return base.LocateViewForModel(modelType);
        }

        public new Type ViewTypeForViewName(string viewName, IEnumerable<Assembly> extraAssemblies)
        {
            return base.ViewTypeForViewName(viewName, Enumerable.Empty<Assembly>());
        }
    }

    private class CreatingAndBindingViewManager : ViewManager
    {
        public UIElement View;
        public object RequestedModel;

        public CreatingAndBindingViewManager(ViewManagerConfig config)
            : base(config) { }

        public override UIElement CreateViewForModel(object model)
        {
            this.RequestedModel = model;
            return this.View;
        }

        public UIElement BindViewToModelView;
        public object BindViewtoModelViewModel;
        public override void BindViewToModel(UIElement view, object viewModel)
        {
            this.BindViewToModelView = view;
            this.BindViewtoModelViewModel = viewModel;
        }
    }

    private class LocatingViewManager : ViewManager
    {
        public LocatingViewManager(ViewManagerConfig config)
            : base(config) { }

        public Type LocatedViewType;
        protected override Type LocateViewForModel(Type modelType)
        {
	             return this.LocatedViewType;
        }
    }

    private class ResolvingViewManager : ViewManager
    {
        public ResolvingViewManager(ViewManagerConfig config) 
           : base(config) { }

        public Type ViewType;
        protected override Type ViewTypeForViewName(string viewName, IEnumerable<Assembly> extraAssemblies)
        {
            return this.ViewType;
        }

        public new Type LocateViewForModel(Type modelType)
        {
            return base.LocateViewForModel(modelType);
        }

        protected override string ViewTypeNameForModelTypeName(string modelTypeName)
        {
            return "testy";
        }
    }


    private class TestView : UIElement
    {
        public bool InitializeComponentCalled;
        public void InitializeComponent()
        {
            this.InitializeComponentCalled = true;
        }
    }

    private class LoadingScreen : Screen
    {
        public int LoadCount { get; private set; }
        public Action CloseAction { get; set; }
        protected override void OnViewLoaded() => this.LoadCount++;
        protected override void OnClose() => this.CloseAction?.Invoke();
    }

    private class LoadingModel : IViewLifecycleAware
    {
        public int LoadCount { get; private set; }
        public event EventHandler ViewReleaseRequested;
        public void ViewLoaded() => this.LoadCount++;
        public void ReleaseView() => this.ViewReleaseRequested?.Invoke(this, EventArgs.Empty);
    }

    private ViewManagerConfig config;
    private AccessibleViewManager viewManager;

    [SetUp]
    public void SetUp()
    {
        this.config = new ViewManagerConfig() { ViewFactory = type => null, ViewAssemblies = new List<Assembly>() };
        this.viewManager = new AccessibleViewManager(this.config);
    }

    [Test]
    public void ViewManagerRejectsNullViewAssemblies()
    {
        Assert.Throws<ArgumentNullException>(() => new ViewManager(new ViewManagerConfig() { ViewFactory = type => null, ViewAssemblies = null }));
        Assert.Throws<ArgumentNullException>(() => this.viewManager.ViewAssemblies = null);
    }

    [Test]
    public void ViewManagerRejectsNullNamespaceTransformations()
    {
        Assert.Throws<ArgumentNullException>(() => this.viewManager.NamespaceTransformations = null);
    }

    [Test]
    public void ViewManagerRejectsNullViewNameSuffix()
    {
        Assert.Throws<ArgumentNullException>(() => this.viewManager.ViewNameSuffix = null);
    }

    [Test]
    public void ViewManagerRejectsNullViewModelNameSuffix()
    {
        Assert.Throws<ArgumentNullException>(() => this.viewManager.ViewModelNameSuffix = null);
    }

    [Test]
    public void ViewManagerRejectsNullViewFactory()
    {
        Assert.Throws<ArgumentNullException>(() => new ViewManager(new ViewManagerConfig() { ViewFactory = null, ViewAssemblies = new List<Assembly>() }));
        Assert.Throws<ArgumentNullException>(() => this.viewManager.ViewFactory = null);
    }

    [Test]
    public void OnModelChangedDoesNothingIfNoChange()
    {
        object val = new();
        this.viewManager.OnModelChanged(null, val, val);
    }

    [Test]
    public void OnModelChangedSetsNullIfNewValueNull()
    {
        var target = new ContentControl();
        this.viewManager.OnModelChanged(target, 5, null);
        Assert.Null(target.Content);
    }

    [Test]
    public void OnModelChangedUsesViewIfAlreadySet()
    {
        var target = new ContentControl();
        var model = new Mock<IViewAware>();
        var view = new UIElement();

        model.Setup(x => x.View).Returns(view);
        this.viewManager.OnModelChanged(target, null, model.Object);

        Assert.AreEqual(view, target.Content);
    }

    [Test]
    public void OnModelChangedCreatesAndBindsView()
    {
        var target = new ContentControl();
        object model = new();
        var view = new UIElement();
        var viewManager = new CreatingAndBindingViewManager(new ViewManagerConfig()
        {
            ViewFactory = type => view,
            ViewAssemblies = new List<Assembly>(),
        });

        viewManager.View = view;

        viewManager.OnModelChanged(target, null, model);

        Assert.AreEqual(viewManager.RequestedModel, model);
        Assert.AreEqual(viewManager.BindViewToModelView, view);
        Assert.AreEqual(viewManager.BindViewtoModelViewModel, model);
        Assert.AreEqual(view, target.Content);
    }

    [Test]
    public void OnModelChangedThrowsIfViewIsAWindow()
    {
        var target = new ContentControl();
        object model = new();
        var view = new Window();
        var viewManager = new CreatingAndBindingViewManager(this.config);

        viewManager.View = view;

        Assert.Throws<StyletInvalidViewTypeException>(() => viewManager.OnModelChanged(target, null, model));
    }

    [Test]
    public void CreateViewForModelReturnsNullIfViewNotFound()
    {
        var viewManager = new AccessibleViewManager(new ViewManagerConfig()
        {
            ViewFactory = type => null,
            ViewAssemblies = new List<Assembly>() { typeof(BootstrapperBase).Assembly, Assembly.GetExecutingAssembly() }
        });
        Assert.IsNull(viewManager.ViewTypeForViewName("Test", Enumerable.Empty<Assembly>()));
    }

    [Test]
    public void LocateViewForModelThrowsIfNameTranslationDoesntWork()
    {
       Assert.Throws<StyletViewLocationException>(() => this.viewManager.LocateViewForModel(typeof(C1)));
    }

    [Test]
    public void LocateViewForModelThrowsIfTypeLocationDoesntWork()
    {
        var viewManager = new ResolvingViewManager(this.config);
        viewManager.ViewType = null;
        Assert.Throws<StyletViewLocationException>(() => viewManager.LocateViewForModel(typeof(C1)));
    }

    [Test]
    public void LocateViewForModelFindsViewForModel()
    {
        var viewManager = new AccessibleViewManager(new ViewManagerConfig() { ViewFactory = type => null, ViewAssemblies = new List<Assembly>() { Assembly.GetExecutingAssembly() } });
        Type viewType = viewManager.LocateViewForModel(typeof(ViewManagerTestsViewModel));
        Assert.AreEqual(typeof(ViewManagerTestsView), viewType);
    }

    [Test]
    public void CreateViewForModelIfNecessaryThrowsIfViewIsNotConcreteUIElement()
    {
        var viewManager = new LocatingViewManager(this.config);

        viewManager.LocatedViewType = typeof(I1);
        Assert.Throws<StyletViewLocationException>(() => viewManager.CreateAndBindViewForModelIfNecessary(new object()));

        viewManager.LocatedViewType = typeof(AC1);
        Assert.Throws<StyletViewLocationException>(() => viewManager.CreateAndBindViewForModelIfNecessary(new object()));

        viewManager.LocatedViewType = typeof(C1);
        Assert.Throws<StyletViewLocationException>(() => viewManager.CreateAndBindViewForModelIfNecessary(new object()));
    }

    [Test]
    public void CreateAndBindViewForModelIfNecessaryCallsFetchesViewAndCallsInitializeComponent()
    {
        var view = new TestView();
        var viewManager = new LocatingViewManager(new ViewManagerConfig()
        {
            ViewFactory = type => view,
            ViewAssemblies = new List<Assembly>(),
        });

        viewManager.LocatedViewType = typeof(TestView);

        UIElement returnedView = viewManager.CreateAndBindViewForModelIfNecessary(new object());

        Assert.True(view.InitializeComponentCalled);
        Assert.AreEqual(view, returnedView);
    }

    [Test]
    public void CreateAndBindViewForModelReturnsViewIfAlreadySet()
    {
        var view = new TestView();
        var viewModel = new Mock<IViewAware>();
        viewModel.SetupGet(x => x.View).Returns(view);

        UIElement returnedView = this.viewManager.CreateAndBindViewForModelIfNecessary(viewModel.Object);

        Assert.AreEqual(view, returnedView);
    }

    [Test]
    public void CreateViewForModelDoesNotComplainIfNoInitializeComponentMethod()
    {
        var view = new UIElement();
        var viewManager = new LocatingViewManager(new ViewManagerConfig()
        {
            ViewFactory = type => view,
            ViewAssemblies = new List<Assembly>(),
        });
        viewManager.LocatedViewType = typeof(UIElement);

        UIElement returnedView = viewManager.CreateAndBindViewForModelIfNecessary(new object());

        Assert.AreEqual(view, returnedView);
    }

    [Test]
    public void BindViewToModelSetsDataContext()
    {
        var view = new FrameworkElement();
        object model = new();
        var viewManager = new AccessibleViewManager(this.config);
        viewManager.BindViewToModel(view, model);

        Assert.AreEqual(model, view.DataContext);
    }

    [Test]
    public void BindViewToModelAttachesView()
    {
        var view = new UIElement();
        var model = new Mock<IViewAware>();
        var viewManager = new AccessibleViewManager(this.config);
        viewManager.BindViewToModel(view, model.Object);

        model.Verify(x => x.AttachView(view));
    }

    [Test]
    public void ScreenViewsAreCachedUntilTheScreenCloses()
    {
        var model = new Screen();
        var manager = new LocatingViewManager(new ViewManagerConfig
        {
            ViewFactory = _ => new FrameworkElement(),
            ViewAssemblies = new List<Assembly>(),
        });
        manager.LocatedViewType = typeof(FrameworkElement);

        UIElement original = manager.CreateAndBindViewForModelIfNecessary(model);
        Assert.AreSame(original, manager.GetView(model));
        Assert.AreSame(original, manager.CreateAndBindViewForModelIfNecessary(model));

        ((IScreenState)model).Close();
        Assert.IsNull(manager.GetView(model));

        ((IScreenState)model).Activate();
        UIElement reopened = manager.CreateAndBindViewForModelIfNecessary(model);
        Assert.AreNotSame(original, reopened);
        Assert.AreSame(reopened, manager.GetView(model));
    }

    [Test]
    public void BindingAnotherViewToTheSameModelIsRejected()
    {
        var model = new Screen();
        var original = new FrameworkElement();
        var replacement = new FrameworkElement();
        this.viewManager.BindViewToModel(original, model);

        Assert.Throws<InvalidOperationException>(() => this.viewManager.BindViewToModel(replacement, model));
        Assert.AreSame(original, this.viewManager.GetView(model));
        Assert.IsNull(replacement.DataContext);
    }

    [Test]
    public void LoadingTheViewNotifiesWithoutActivatingTheScreen()
    {
        var model = new LoadingScreen();
        var view = new FrameworkElement();
        this.viewManager.BindViewToModel(view, model);
        this.viewManager.BindViewToModel(view, model);

        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        Assert.AreEqual(1, model.LoadCount);
        Assert.AreEqual(ScreenState.Deactivated, model.ScreenState);
    }

    [Test]
    public void ClosingTheScreenRemovesTheOldLoadedCallback()
    {
        var model = new LoadingScreen();
        var original = new FrameworkElement();
        this.viewManager.BindViewToModel(original, model);
        ((IScreenState)model).Close();

        var replacement = new FrameworkElement();
        ((IScreenState)model).Activate();
        this.viewManager.BindViewToModel(replacement, model);
        original.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.AreEqual(0, model.LoadCount);

        replacement.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.AreEqual(1, model.LoadCount);
        Assert.AreSame(replacement, this.viewManager.GetView(model));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClosingCallbacksCanConnectANewView(bool reconnectFromClosedEvent)
    {
        var model = new LoadingScreen();
        var manager = new LocatingViewManager(new ViewManagerConfig
        {
            ViewFactory = _ => new FrameworkElement(),
            ViewAssemblies = new List<Assembly>(),
        });
        manager.LocatedViewType = typeof(FrameworkElement);
        FrameworkElement replacement = null;
        void Reconnect()
        {
            Assert.IsNull(manager.GetView(model));
            replacement = (FrameworkElement)manager.CreateAndBindViewForModelIfNecessary(model);
        }

        // 기존 View의 연결보다 먼저 등록된 종료 이벤트에서도 재연결할 수 있어야 합니다.
        if (reconnectFromClosedEvent)
            model.Closed += (_, _) => Reconnect();
        else
            model.CloseAction = Reconnect;

        var original = (FrameworkElement)manager.CreateAndBindViewForModelIfNecessary(model);
        ((IScreenState)model).Close();

        Assert.IsNotNull(replacement);
        Assert.AreNotSame(original, replacement);
        Assert.AreSame(replacement, manager.GetView(model));
        original.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.AreEqual(0, model.LoadCount);
        replacement.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.AreEqual(1, model.LoadCount);
    }

    [Test]
    public void ClosingOnAWorkerDoesNotWaitForTheUiThread()
    {
        var model = new LoadingScreen();
        var view = new FrameworkElement();
        this.viewManager.BindViewToModel(view, model);

        Task close = Task.Run(() => ((IScreenState)model).Close());
        Assert.IsTrue(close.Wait(TimeSpan.FromSeconds(5)), "View 해제가 UI 스레드의 동기 처리를 기다립니다.");

        Assert.IsNull(this.viewManager.GetView(model));
        // UI 이벤트 구독 해제가 큐에 남아 있어도 이전 로딩 알림은 무시합니다.
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.AreEqual(0, model.LoadCount);
    }

    [Test]
    public void ScreenStateOnlyModelsReleaseTheirViewOnClosed()
    {
        var model = new Mock<IScreenState>();
        this.viewManager.BindViewToModel(new FrameworkElement(), model.Object);
        Assert.IsNotNull(this.viewManager.GetView(model.Object));

        model.Raise(x => x.Closed += null, new CloseEventArgs(ScreenState.Deactivated));

        Assert.IsNull(this.viewManager.GetView(model.Object));
    }

    [Test]
    public void LifecycleOnlyModelsCanReleaseTheirViewCallbacks()
    {
        var model = new LoadingModel();
        var first = new FrameworkElement();
        var second = new FrameworkElement();
        this.viewManager.BindViewToModel(first, model);
        this.viewManager.BindViewToModel(second, model);

        model.ReleaseView();
        first.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        second.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        Assert.AreEqual(0, model.LoadCount);
        Assert.IsNull(this.viewManager.GetView(model));
    }

    [Test]
    public void ClosingAWindowRemovesTheLifecycleOnlyModelsLoadedCallback()
    {
        var model = new LoadingModel();
        var window = new Window();
        this.viewManager.BindViewToModel(window, model);
        Assert.IsNull(this.viewManager.GetView(model));

        window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.AreEqual(1, model.LoadCount);

        window.Close();
        window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        Assert.AreEqual(1, model.LoadCount);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RegularViewModelsCanBeShownInMultipleContentControls(bool notifyLoading)
    {
        object model = notifyLoading ? new LoadingModel() : new object();
        var manager = new LocatingViewManager(new ViewManagerConfig
        {
            ViewFactory = _ => new FrameworkElement(),
            ViewAssemblies = new List<Assembly>(),
        });
        manager.LocatedViewType = typeof(FrameworkElement);
        var firstContainer = new ContentControl();
        var secondContainer = new ContentControl();

        manager.OnModelChanged(firstContainer, null, model);
        manager.OnModelChanged(secondContainer, null, model);

        var first = (FrameworkElement)firstContainer.Content;
        var second = (FrameworkElement)secondContainer.Content;
        Assert.AreNotSame(first, second);
        Assert.AreSame(model, first.DataContext);
        Assert.AreSame(model, second.DataContext);
        Assert.IsNull(manager.GetView(model));

        if (model is LoadingModel loadingModel)
        {
            first.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            second.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.AreEqual(2, loadingModel.LoadCount);
        }
    }

    [Test]
    public void UnconnectedModelsHaveNoView()
    {
        Assert.IsNull(this.viewManager.GetView(new Screen()));
        Assert.IsNull(this.viewManager.GetView(null));
    }

    [Test]
    public void CachedViewsDoNotKeepUnusedViewModelsAlive()
    {
        WeakReference<Screen> model = CreateUnreferencedScreen(this.viewManager);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(model.TryGetTarget(out _));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Screen> CreateUnreferencedScreen(IViewManager manager)
    {
        var model = new Screen();
        manager.BindViewToModel(new FrameworkElement(), model);
        return new WeakReference<Screen>(model);
    }

    [Test]
    public void ViewNameResolutionWorksAsExpected()
    {
        Assert.AreEqual("Root.Test.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Root.Test.ThingViewModel"));
        Assert.AreEqual("Root.Views.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Root.ViewModels.ThingViewModel"));
        Assert.AreEqual("Root.View.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Root.ViewModel.ThingViewModel"));
        Assert.AreEqual("Root.View.ViewModelThing", this.viewManager.ViewTypeNameForModelTypeName("Root.ViewModel.ViewModelThing"));
        Assert.AreEqual("Root.ThingViews.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Root.ThingViewModels.ThingViewModel"));
        Assert.AreEqual("Root.ThingView.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Root.ThingViewModel.ThingViewModel"));

        Assert.AreEqual("Root.ViewModelsNamespace.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Root.ViewModelsNamespace.ThingViewModel"));
        Assert.AreEqual("Root.ViewModelNamespace.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Root.ViewModelNamespace.ThingViewModel"));
        Assert.AreEqual("Root.NamespaceOfViews.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Root.NamespaceOfViewModels.ThingViewModel"));
        Assert.AreEqual("Root.NamespaceOfView.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Root.NamespaceOfViewModel.ThingViewModel"));

        Assert.AreEqual("ViewModels.TestView", this.viewManager.ViewTypeNameForModelTypeName("ViewModels.TestViewModel"));
    }

    [Test]
    public void ViewNameResolutionUsesConfig()
    {
        this.viewManager.ViewNameSuffix = "Viiiiew";
        this.viewManager.ViewModelNameSuffix = "ViiiiiewModel";

        Assert.AreEqual("Root.Test.ThingViiiiew", this.viewManager.ViewTypeNameForModelTypeName("Root.Test.ThingViiiiiewModel"));
    }

    [Test]
    public void NamespaceTransformationsTransformsNamespace()
    {
        this.viewManager.NamespaceTransformations["Foo.Bar"] = "Baz.Yay";

        Assert.AreEqual("Baz.Yay.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Foo.Bar.ThingViewModel"));
        Assert.AreEqual("Baz.Yay.Thing", this.viewManager.ViewTypeNameForModelTypeName("Foo.Bar.Thing"));
    }

    [Test]
    public void NamespaceTransformationsTransformOnlyFirstMatch()
    {
        this.viewManager.NamespaceTransformations["Foo.Bar"] = "Baz.Yay";
        this.viewManager.NamespaceTransformations["Baz.Yay"] = "One.Two";

        Assert.AreEqual("Baz.Yay.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Foo.Bar.ThingViewModel"));
        Assert.AreEqual("One.Two.ThingView", this.viewManager.ViewTypeNameForModelTypeName("Baz.Yay.ThingViewModel"));
    }
}
