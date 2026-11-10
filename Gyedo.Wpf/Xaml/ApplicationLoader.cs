using System;
using System.Windows;

namespace Gyedo.Xaml;

/// <summary>
/// Added to your App.xaml, this is responsible for loading the Boostrapper you specify, and Gyedo's other resources
/// </summary>
public class ApplicationLoader : ResourceDictionary
{
    private readonly ResourceDictionary gyedoResourceDictionary;

    /// <summary>
    /// Initialises a new instance of the <see cref="ApplicationLoader"/> class
    /// </summary>
    public ApplicationLoader()
    {
        this.gyedoResourceDictionary = new ResourceDictionary() { Source = new Uri("pack://application:,,,/Gyedo.Wpf;component/Xaml/GyedoResourceDictionary.xaml", UriKind.Absolute) };
        this.LoadGyedoResources = true;
    }

    private IBootstrapper _bootstrapper;

    /// <summary>
    /// Gets or sets the bootstrapper instance to use to start your application. This must be set.
    /// </summary>
    public IBootstrapper Bootstrapper
    {
        get => this._bootstrapper;
        set
        {
            this._bootstrapper = value;
            this._bootstrapper.Setup(Application.Current);
        }
    }

    private bool _loadGyedoResources;

    /// <summary>
    /// Gets or sets a value indicating whether to load Gyedo's own resources (e.g. GyedoConductorTabControl). Defaults to true.
    /// </summary>
    public bool LoadGyedoResources
    {
        get => this._loadGyedoResources;
        set
        {
            this._loadGyedoResources = value;
            if (this._loadGyedoResources)
                this.MergedDictionaries.Add(this.gyedoResourceDictionary);
            else
                this.MergedDictionaries.Remove(this.gyedoResourceDictionary);
        }
    }
}
