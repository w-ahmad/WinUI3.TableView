using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace SampleApp;

public sealed partial class MainWindow : Window
{
    static bool _loaded = false;
    readonly List<DataGridDataItem> _items = new();
    readonly ObservableCollection<string> _mountains = new();

    public MainWindow()
    {
        InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        SetTitleBar(CustomTitleBar);
        CreateGradientBackdrop(root, new System.Numerics.Vector2(0.9f, 1));
    }

    async void OnRootGridLoaded(object sender, RoutedEventArgs e)
    {
        if (!File.Exists("Assets\\mtns.csv"))
        {
            await App.ShowDialogBox("Warning", $"The CSV data is missing from this location:{Environment.NewLine}{AppContext.BaseDirectory}", "OK", "", null, null, new Uri($"ms-appx:///Assets/Warning.png"));
            return;
        }

        if (App.AnimationsEffectsEnabled)
            StoryboardSpinner.Begin();

        #region [Load file data]
        var lines = await File.ReadAllLinesAsync("Assets\\mtns.csv");
        foreach (var line in lines)
        {
            try
            {
                var values = line.Split(',');
                _items.Add(new DataGridDataItem()
                    {
                        Rank = uint.Parse(values[0]),
                        Mountain = values[1],
                        Height_m = uint.Parse(values[2]),
                        Range = values[3],
                        Coordinates = values[4],
                        Prominence = uint.Parse(values[5]),
                        Parent_mountain = values[6],
                        First_ascent = DateTimeOffset.Parse(values[7], CultureInfo.InvariantCulture.DateTimeFormat),
                        Ascents = values[8],
                    });
                _mountains.Add(values[1]);
            }
            catch (Exception) { }
        }
        #endregion

        ReloadItems();
    }

    void OnLoadMoreButtonClick(object sender, RoutedEventArgs e)
    {
        var collection = (ObservableCollection<DataGridDataItem>)tableView.ItemsSource;
        _items.ForEach(collection.Add);
    }

    void OnClearAndLoadButtonClick(object sender, RoutedEventArgs e)
    {
        ReloadItems();
    }

    /// <summary>
    /// We'll need to reload the <see cref="WinUI.TableView.TableViewCell"/>s to observe the toggle change.
    /// </summary>
    void SingleClickEditOnToggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
            return;

        ReloadItems();
    }

    void ReloadItems()
    {
        // Trying to prevent the stall by spinning this off on another thread.
        Task.Run(async () =>
        {
            await Task.Delay(20); // Allow button/toggle animation to finish.
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () => 
            {
                imgSpinner.Visibility = Visibility.Visible;
            });
        })
        .ContinueWith((t) => 
        { 
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
            {
                tableView.ItemsSource = new ObservableCollection<DataGridDataItem>(_items);
                await Task.Delay(1500);
                imgSpinner.Visibility = Visibility.Collapsed;
            });
            _loaded = true;
        });
    }

    void CreateGradientBackdrop(FrameworkElement fe, System.Numerics.Vector2 endPoint)
    {
        // Get the FrameworkElement's compositor.
        var compositor = ElementCompositionPreview.GetElementVisual(fe).Compositor;
        if (compositor == null) { return; }
        var gb = compositor.CreateLinearGradientBrush();

        // Define gradient stops.
        var gradientStops = gb.ColorStops;

        // If we found our App.xaml brushes then use them.
        if (App.Current.Resources.TryGetValue("GC1", out object clr1) &&
            App.Current.Resources.TryGetValue("GC2", out object clr2) &&
            App.Current.Resources.TryGetValue("GC3", out object clr3) &&
            App.Current.Resources.TryGetValue("GC4", out object clr4))
        {
            gradientStops.Insert(0, compositor.CreateColorGradientStop(0.0f, (Windows.UI.Color)clr1));
            gradientStops.Insert(1, compositor.CreateColorGradientStop(0.5f, (Windows.UI.Color)clr2));
            gradientStops.Insert(2, compositor.CreateColorGradientStop(0.75f, (Windows.UI.Color)clr3));
            gradientStops.Insert(3, compositor.CreateColorGradientStop(1.0f, (Windows.UI.Color)clr4));
        }
        else
        {
            gradientStops.Insert(0, compositor.CreateColorGradientStop(0.0f, Windows.UI.Color.FromArgb(55, 255, 0, 0)));   // Red
            gradientStops.Insert(1, compositor.CreateColorGradientStop(0.3f, Windows.UI.Color.FromArgb(55, 255, 216, 0))); // Yellow
            gradientStops.Insert(2, compositor.CreateColorGradientStop(0.6f, Windows.UI.Color.FromArgb(55, 0, 255, 0)));   // Green
            gradientStops.Insert(3, compositor.CreateColorGradientStop(1.0f, Windows.UI.Color.FromArgb(55, 0, 0, 255)));   // Blue
        }

        // Set the direction of the gradient.
        gb.StartPoint = new System.Numerics.Vector2(0, 0);
        //gb.EndPoint = new System.Numerics.Vector2(1, 1);
        gb.EndPoint = endPoint;

        // Create a sprite visual and assign the gradient brush.
        var spriteVisual = Compositor.CreateSpriteVisual();
        spriteVisual.Brush = gb;

        // Set the size of the sprite visual to cover the entire window.
        spriteVisual.Size = new System.Numerics.Vector2((float)fe.ActualSize.X, (float)fe.ActualSize.Y);

        // Handle the SizeChanged event to adjust the size of the sprite visual when the window is resized.
        fe.SizeChanged += (s, e) =>
        {
            spriteVisual.Size = new System.Numerics.Vector2((float)fe.ActualWidth, (float)fe.ActualHeight);
        };

        // Set the sprite visual as the background of the FrameworkElement.
        ElementCompositionPreview.SetElementChildVisual(fe, spriteVisual);
    }
}
