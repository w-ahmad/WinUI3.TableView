﻿using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Windows.Foundation;
using WinUI.TableView.Converters;

namespace WinUI.TableView;

[TemplateVisualState(Name = VisualStates.StateNormal, GroupName = VisualStates.GroupCommon)]
[TemplateVisualState(Name = VisualStates.StateSelectAllButton, GroupName = VisualStates.GroupSelectAllButton)]
[TemplateVisualState(Name = VisualStates.StateSelectAllCheckBox, GroupName = VisualStates.GroupSelectAllButton)]
[TemplateVisualState(Name = VisualStates.StateOptionsButton, GroupName = VisualStates.GroupSelectAllButton)]
public partial class TableViewHeaderRow : Control
{
    private Button? _optionsButton;
    private CheckBox? _selectAllCheckBox;
    private Rectangle? _v_gridLine;
    private Rectangle? _h_gridLine;
    private StackPanel? _headersStackPanel;
    private bool _calculatingHeaderWidths;
    private DispatcherTimer? _timer;

    public TableViewHeaderRow()
    {
        DefaultStyleKey = typeof(TableViewHeaderRow);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _optionsButton = GetTemplateChild("optionsButton") as Button;
        _selectAllCheckBox = GetTemplateChild("selectAllCheckBox") as CheckBox;
        _v_gridLine = GetTemplateChild("VerticalGridLine") as Rectangle;
        _h_gridLine = GetTemplateChild("HorizontalGridLine") as Rectangle;
        TableView?.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, delegate { SetSelectAllButtonState(); });
        TableView?.RegisterPropertyChangedCallback(TableView.ShowOptionsButtonProperty, delegate { SetSelectAllButtonState(); });
        TableView?.RegisterPropertyChangedCallback(TableView.ItemsSourceProperty, delegate { OnTableViewSelectionChanged(); });

        if (TableView is null)
        {
            return;
        }

        TableView.SelectionChanged += delegate { OnTableViewSelectionChanged(); };
        TableView.Items.VectorChanged += delegate { OnTableViewSelectionChanged(); };

        if (_optionsButton is not null)
        {
            _optionsButton.DataContext = new OptionsFlyoutViewModel(TableView);
        }

        if (GetTemplateChild("selectAllButton") is Button selectAllButton)
        {
            selectAllButton.Tapped += delegate { TableView.SelectAllSafe(); };
        }

        if (_selectAllCheckBox is not null)
        {
            _selectAllCheckBox.Checked += OnSelectAllCheckBoxChecked;
            _selectAllCheckBox.Unchecked += OnSelectAllCheckBoxUnchecked;
        }

        if (TableView.Columns is not null && GetTemplateChild("HeadersStackPanel") is StackPanel stackPanel)
        {
            _headersStackPanel = stackPanel;
            AddHeaders(TableView.Columns.VisibleColumns);

            TableView.Columns.CollectionChanged += OnTableViewColumnsCollectionChanged;
            TableView.Columns.ColumnPropertyChanged += OnColumnPropertyChanged;
        }

        SetExportOptionsVisibility();
        SetSelectAllButtonState();
        EnsureGridLines();
    }

    private void OnTableViewColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.OfType<TableViewColumn>() is IEnumerable<TableViewColumn> newItems)
        {
            AddHeaders(newItems.Where(x => x.Visibility == Visibility.Visible), e.NewStartingIndex);
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems?.OfType<TableViewColumn>() is IEnumerable<TableViewColumn> oldItems)
        {
            RemoveHeaders(oldItems);
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset && _headersStackPanel is not null)
        {
            _headersStackPanel.Children.Clear();
        }
    }

    private void OnColumnPropertyChanged(object? sender, TableViewColumnPropertyChanged e)
    {
        if (e.PropertyName is nameof(TableViewColumn.Visibility))
        {
            if (e.Column.Visibility == Visibility.Visible)
            {
                AddHeaders(new[] { e.Column }, e.Index);
            }
            else
            {
                RemoveHeaders(new[] { e.Column });
            }
        }
        else if (e.PropertyName is nameof(TableViewColumn.Width) or
                                   nameof(TableViewColumn.MinWidth) or
                                   nameof(TableViewColumn.MaxWidth))
        {
            if (!_calculatingHeaderWidths)
            {
                CalculateHeaderWidths();
            }
        }
        else if (e.PropertyName is nameof(TableViewColumn.DesiredWidth))
        {
            if (_timer is null)
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                _timer.Tick += OnTimerTick;
            }

            _timer.Stop();
            _timer.Start();
        }
    }

    private void OnTimerTick(object? sender, object e)
    {
        if (!_calculatingHeaderWidths)
        {
            CalculateHeaderWidths();
        }

        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }
    }

    private void RemoveHeaders(IEnumerable<TableViewColumn> columns)
    {
        if (_headersStackPanel is not null)
        {
            foreach (var column in columns)
            {
                var header = _headersStackPanel.Children.OfType<TableViewColumnHeader>().FirstOrDefault(x => x == column.HeaderControl);
                if (header is not null)
                {
                    _headersStackPanel.Children.Remove(header);
                }
            }
        }
    }

    private void AddHeaders(IEnumerable<TableViewColumn> columns, int index = -1)
    {
        if (_headersStackPanel is not null)
        {
            foreach (var column in columns)
            {
                var header = new TableViewColumnHeader { DataContext = column, Column = column };
                column.HeaderControl = header;

                if (index < 0)
                {
                    _headersStackPanel.Children.Add(header);
                }
                else
                {
                    index = Math.Min(index, _headersStackPanel.Children.Count);
                    _headersStackPanel.Children.Insert(index, header);
                    index++;
                }

                header.SetBinding(ContentControl.ContentProperty,
                                  new Binding { Path = new PropertyPath(nameof(TableViewColumn.Header)) });
            }

            CalculateHeaderWidths();
        }
    }

    internal void CalculateHeaderWidths()
    {
        if (TableView?.ActualWidth > 0)
        {
            _calculatingHeaderWidths = true;

            var allColumns = TableView.Columns.VisibleColumns.ToList();
            var starColumns = allColumns.Where(x => x.Width.IsStar).ToList();
            var autoColumns = allColumns.Where(x => x.Width.IsAuto).ToList();
            var absoluteColumns = allColumns.Where(x => x.Width.IsAbsolute).ToList();

            var height = TableView.HeaderRowHeight;
            var availableWidth = TableView.ActualWidth - 32;
            var starUnitWeight = starColumns.Select(x => x.Width.Value).Sum();
            var fixedWidth = autoColumns.Select(x =>
            {
                if (x.HeaderControl is { } header)
                {
                    header.Measure(new Size(double.PositiveInfinity, height));
                    return Math.Max(x.DesiredWidth, header.DesiredSize.Width);
                }

                return x.DesiredWidth;
            }).Sum();
            fixedWidth += absoluteColumns.Select(x => x.ActualWidth).Sum();

            availableWidth -= fixedWidth;
            var starUnitWidth = starUnitWeight > 0 ? availableWidth / starUnitWeight : 0;

            var starWidthAdjusted = false;
            var startColumnsToAdjust = starColumns.ToList();
            do
            {
                starWidthAdjusted = false;

                foreach (var column in startColumnsToAdjust)
                {
                    if (column.HeaderControl is { } header)
                    {
                        var width = starUnitWidth * column.Width.Value;
                        var minWidth = column.MinWidth ?? TableView.MinColumnWidth;
                        var maxWidth = column.MaxWidth ?? TableView.MaxColumnWidth;

                        width = width < minWidth ? minWidth : width;
                        width = width > maxWidth ? maxWidth : width;

                        if (width != starUnitWidth * column.Width.Value)
                        {
                            availableWidth -= width;
                            starUnitWeight -= column.Width.Value;
                            starUnitWidth = starUnitWeight > 0 ? availableWidth / starUnitWeight : 0;
                            startColumnsToAdjust.Remove(column);
                            starWidthAdjusted = true;
                            break;
                        }
                    }
                }

            } while (starWidthAdjusted);


            foreach (var column in allColumns)
            {
                if (column.HeaderControl is { } header)
                {
                    var width = column.Width.IsStar
                                ? starUnitWidth * column.Width.Value
                                : column.Width.IsAbsolute ? column.Width.Value
                                : Math.Max(header.DesiredSize.Width, column.DesiredWidth);

                    var minWidth = column.MinWidth ?? TableView.MinColumnWidth;
                    var maxWidth = column.MaxWidth ?? TableView.MaxColumnWidth;

                    width = width < minWidth ? minWidth : width;
                    width = width > maxWidth ? maxWidth : width;
                    header.Width = width;
                    header.MaxWidth = width;

                    DispatcherQueue.TryEnqueue(() =>
                        header.Measure(
                            new Size(header.Width,
                            _headersStackPanel?.ActualHeight ?? TableView.HeaderRowHeight)));
                }
            }

            _calculatingHeaderWidths = false;
        }
    }

    private void SetExportOptionsVisibility()
    {
        var binding = new Binding
        {
            Path = new PropertyPath(nameof(TableView.ShowExportOptions)),
            Source = TableView,
            Converter = new BoolToVisibilityConverter()
        };

        if (GetTemplateChild("ExportAllMenuItem") is MenuFlyoutItem exportAll)
        {
            exportAll.SetBinding(VisibilityProperty, binding);
        }

        if (GetTemplateChild("ExportSelectedMenuItem") is MenuFlyoutItem exportSelected)
        {
            exportSelected.SetBinding(VisibilityProperty, binding);
        }
    }

    private void OnTableViewSelectionChanged()
    {
        if (TableView is not null && _selectAllCheckBox is not null)
        {
            if (TableView.Items.Count == 0)
            {
                _selectAllCheckBox.IsChecked = null;
                _selectAllCheckBox.IsEnabled = false;
            }
            else if (TableView.SelectedItems.Count == TableView.Items.Count)
            {
                _selectAllCheckBox.IsChecked = true;
                _selectAllCheckBox.IsEnabled = true;
            }
            else if (TableView.SelectedItems.Count > 0)
            {
                _selectAllCheckBox.IsChecked = null;
                _selectAllCheckBox.IsEnabled = true;
            }
            else
            {
                _selectAllCheckBox.IsChecked = false;
                _selectAllCheckBox.IsEnabled = true;
            }
        }
    }

    private void SetSelectAllButtonState()
    {
        if (TableView?.SelectionMode == ListViewSelectionMode.Multiple)
        {
            VisualStates.GoToState(this, false, VisualStates.StateSelectAllCheckBox);
        }
        else if (TableView?.ShowOptionsButton is true)
        {
            VisualStates.GoToState(this, false, VisualStates.StateOptionsButton);
        }
        else
        {
            VisualStates.GoToState(this, false, VisualStates.StateSelectAllButton);
        }
    }

    private void OnSelectAllCheckBoxChecked(object sender, RoutedEventArgs e)
    {
        TableView?.SelectAllSafe();
    }

    private void OnSelectAllCheckBoxUnchecked(object sender, RoutedEventArgs e)
    {
        TableView?.DeselectAll();
    }

    internal void EnsureGridLines()
    {
        if (_h_gridLine is not null && TableView is not null)
        {
            _h_gridLine.Fill = TableView.HorizontalGridLinesStroke;
            _h_gridLine.Height = TableView.HorizontalGridLinesStrokeThickness;
            _h_gridLine.Visibility = TableView.HeaderGridLinesVisibility is TableViewGridLinesVisibility.All or TableViewGridLinesVisibility.Horizontal
                                     ? Visibility.Visible : Visibility.Collapsed;

            if (_v_gridLine is not null)
            {
                _v_gridLine.Fill = TableView.HeaderGridLinesVisibility is TableViewGridLinesVisibility.All or TableViewGridLinesVisibility.Vertical
                                   ? TableView.VerticalGridLinesStroke : new SolidColorBrush(Colors.Transparent);
                _v_gridLine.Width = TableView.VerticalGridLinesStrokeThickness;
                _v_gridLine.Visibility = TableView.HeaderGridLinesVisibility is TableViewGridLinesVisibility.All or TableViewGridLinesVisibility.Vertical
                                         || TableView.GridLinesVisibility is TableViewGridLinesVisibility.All or TableViewGridLinesVisibility.Vertical
                                         ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        foreach (var header in Headers)
        {
            header.EnsureGridLines();
        }

        if (!_calculatingHeaderWidths)
        {
            CalculateHeaderWidths();
        }
    }

    private IList<TableViewColumnHeader> Headers => _headersStackPanel!.Children.OfType<TableViewColumnHeader>().ToList();

    internal TableViewColumnHeader? GetPreviousHeader(TableViewColumnHeader? currentHeader)
    {
        var previousCellIndex = currentHeader is null ? Headers.Count - 1 : Headers.IndexOf(currentHeader) - 1;
        return previousCellIndex >= 0 ? Headers[previousCellIndex] : default;
    }

    private void OnTableViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        CalculateHeaderWidths();
    }

    private static void OnTableViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableViewHeaderRow headerRow)
        {
            if (e.OldValue is TableView oldTableView)
            {
                oldTableView.SizeChanged -= headerRow.OnTableViewSizeChanged;
            }

            if (e.NewValue is TableView newTableView)
            {
                newTableView.SizeChanged += headerRow.OnTableViewSizeChanged;
            }
        }
    }

    public TableView? TableView
    {
        get => (TableView?)GetValue(TableViewProperty);
        set => SetValue(TableViewProperty, value);
    }

    public static readonly DependencyProperty TableViewProperty = DependencyProperty.Register(nameof(TableView), typeof(TableView), typeof(TableViewHeaderRow), new PropertyMetadata(null, OnTableViewChanged));
}
