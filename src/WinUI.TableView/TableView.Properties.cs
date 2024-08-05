using CommunityToolkit.WinUI.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WinUI.TableView;
public partial class TableView
{
    public static readonly new DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IList), typeof(TableView), new PropertyMetadata(null, OnItemsSourceChanged));
    public static readonly new DependencyProperty SelectionModeProperty = DependencyProperty.Register(nameof(SelectionMode), typeof(ListViewSelectionMode), typeof(TableView), new PropertyMetadata(ListViewSelectionMode.Extended, OnSelectionModeChanged));
    public static readonly DependencyProperty HeaderRowHeightProperty = DependencyProperty.Register(nameof(HeaderRowHeight), typeof(double), typeof(TableView), new PropertyMetadata(32d, OnHeaderRowHeightChanged));
    public static readonly DependencyProperty RowHeightProperty = DependencyProperty.Register(nameof(RowHeight), typeof(double), typeof(TableView), new PropertyMetadata(40d));
    public static readonly DependencyProperty RowMaxHeightProperty = DependencyProperty.Register(nameof(RowMaxHeight), typeof(double), typeof(TableView), new PropertyMetadata(double.PositiveInfinity));
    public static readonly DependencyProperty ShowExportOptionsProperty = DependencyProperty.Register(nameof(ShowExportOptions), typeof(bool), typeof(TableView), new PropertyMetadata(false));
    public static readonly DependencyProperty AutoGenerateColumnsProperty = DependencyProperty.Register(nameof(AutoGenerateColumns), typeof(bool), typeof(TableView), new PropertyMetadata(true, OnAutoGenerateColumnsChanged));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(TableView), new PropertyMetadata(false));
    public static readonly DependencyProperty ShowOptionsButtonProperty = DependencyProperty.Register(nameof(ShowOptionsButton), typeof(bool), typeof(TableView), new PropertyMetadata(true));
    public static readonly DependencyProperty CanResizeColumnsProperty = DependencyProperty.Register(nameof(CanResizeColumns), typeof(bool), typeof(TableView), new PropertyMetadata(true));
    public static readonly DependencyProperty CanSortColumnsProperty = DependencyProperty.Register(nameof(CanSortColumns), typeof(bool), typeof(TableView), new PropertyMetadata(true, OnCanSortColumnsChanged));
    public static readonly DependencyProperty CanFilterColumnsProperty = DependencyProperty.Register(nameof(CanFilterColumns), typeof(bool), typeof(TableView), new PropertyMetadata(true, OnCanFilterColumnsChanged));
    public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(nameof(MinColumnWidth), typeof(double), typeof(TableView), new PropertyMetadata(50d, OnMinColumnWidthChanged));
    public static readonly DependencyProperty MaxColumnWidthProperty = DependencyProperty.Register(nameof(MaxColumnWidth), typeof(double), typeof(TableView), new PropertyMetadata(double.PositiveInfinity, OnMaxColumnWidthChanged));
    public static readonly DependencyProperty SelectionUnitProperty = DependencyProperty.Register(nameof(SelectionUnit), typeof(TableViewSelectionUnit), typeof(TableView), new PropertyMetadata(TableViewSelectionUnit.CellOrRow, OnSelectionUnitChanged));
    public static readonly DependencyProperty SingleClickEditingProperty = DependencyProperty.Register(nameof(SingleClickEditing), typeof(bool), typeof(TableView), new PropertyMetadata(false));

    public IAdvancedCollectionView CollectionView { get; private set; } = new AdvancedCollectionView();
    internal IDictionary<string, Predicate<object>> ActiveFilters { get; } = new Dictionary<string, Predicate<object>>();
    internal TableViewCellSlot? CurrentCellSlot { get; set; }
    internal TableViewCellSlot? SelectionStartCellSlot { get; set; }
    internal HashSet<TableViewCellSlot> SelectedCells { get; set; } = new HashSet<TableViewCellSlot>();
    internal HashSet<HashSet<TableViewCellSlot>> SelectedCellRanges { get; } = new HashSet<HashSet<TableViewCellSlot>>();
    internal bool IsEditing { get; set; }
    public TableViewColumnsCollection Columns { get; } = new();

    public double HeaderRowHeight
    {
        get => (double)GetValue(HeaderRowHeightProperty);
        set => SetValue(HeaderRowHeightProperty, value);
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public double RowMaxHeight
    {
        get => (double)GetValue(RowMaxHeightProperty);
        set => SetValue(RowMaxHeightProperty, value);
    }

    public new IList ItemsSource
    {
        get => (IList)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public new ListViewSelectionMode SelectionMode
    {
        get => (ListViewSelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public bool ShowExportOptions
    {
        get => (bool)GetValue(ShowExportOptionsProperty);
        set => SetValue(ShowExportOptionsProperty, value);
    }

    public bool AutoGenerateColumns
    {
        get => (bool)GetValue(AutoGenerateColumnsProperty);
        set => SetValue(AutoGenerateColumnsProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool ShowOptionsButton
    {
        get => (bool)GetValue(ShowOptionsButtonProperty);
        set => SetValue(ShowOptionsButtonProperty, value);
    }

    public bool CanResizeColumns
    {
        get => (bool)GetValue(CanResizeColumnsProperty);
        set => SetValue(CanResizeColumnsProperty, value);
    }

    public bool CanSortColumns
    {
        get => (bool)GetValue(CanSortColumnsProperty);
        set => SetValue(CanSortColumnsProperty, value);
    }

    public bool CanFilterColumns
    {
        get => (bool)GetValue(CanFilterColumnsProperty);
        set => SetValue(CanFilterColumnsProperty, value);
    }

    public bool SingleClickEditing
    {
        get => (bool)GetValue(SingleClickEditingProperty);
        set => SetValue(SingleClickEditingProperty, value);
    }

    public double MinColumnWidth
    {
        get => (double)GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public double MaxColumnWidth
    {
        get => (double)GetValue(MaxColumnWidthProperty);
        set => SetValue(MaxColumnWidthProperty, value);
    }

    public TableViewSelectionUnit SelectionUnit
    {
        get => (TableViewSelectionUnit)GetValue(SelectionUnitProperty);
        set => SetValue(SelectionUnitProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableView tableView)
        {
            tableView.OnItemsSourceChanged(e);
            tableView.SelectedCellRanges.Clear();
            tableView.OnCellSelectionChanged();
        }
    }

    private static void OnSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableView tableView)
        {
            if (tableView.SelectionMode is ListViewSelectionMode.Single or ListViewSelectionMode.None)
            {
                var currentCell = tableView.CurrentCellSlot.HasValue ? tableView.GetCellFromSlot(tableView.CurrentCellSlot.Value) : default;
                currentCell?.ApplyCurrentCellState();
                tableView.SelectedCellRanges.Clear();

                if (tableView.SelectionMode is ListViewSelectionMode.Single && tableView.CurrentCellSlot.HasValue)
                {
                    tableView.SelectedCellRanges.Add(new() { tableView.CurrentCellSlot.Value });
                }

                tableView.OnCellSelectionChanged();
            }

            tableView.UpdateBaseSelectionMode();
        }
    }

    private static void OnHeaderRowHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as TableView)?.UpdateVerticalScrollBarMargin();
    }

    private static void OnAutoGenerateColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableView tableView)
        {
            if (tableView.AutoGenerateColumns)
            {
                tableView.GenerateColumns();
            }
            else
            {
                tableView.RemoveAutoGeneratedColumns();
            }
        }
    }

    private static void OnCanSortColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableView tableView && e.NewValue is false)
        {
            tableView.ClearSorting();
        }
    }

    private static void OnCanFilterColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableView tableView && e.NewValue is false)
        {
            tableView.ClearFilters();
        }
    }

    private static void OnMinColumnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableView table && table._headerRow is not null)
        {
            table._headerRow.CalculateHeaderWidths();
        }
    }

    private static void OnMaxColumnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableView table && table._headerRow is not null)
        {
            table._headerRow.CalculateHeaderWidths();
        }
    }

    private static void OnSelectionUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableView tableView)
        {
            if (tableView.SelectionUnit is TableViewSelectionUnit.Row)
            {
                tableView.SelectedCellRanges.Clear();
                tableView.OnCellSelectionChanged();
            }

            tableView.UpdateBaseSelectionMode();
        }
    }

    private void OnBaseItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
    {
        throw new InvalidOperationException("Setting this property directly is not allowed. Use TableView.ItemsSource instead.");
    }

    private void OnBaseSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (!_shouldThrowSelectionModeChangedException)
        {
            throw new InvalidOperationException("Setting this property directly is not allowed. Use TableView.SelectionMode instead.");
        }
    }
}

public static class ItemIndexRangeExtensions
{
    public static bool IsInRange(this ItemIndexRange range, int index)
    {
        return index >= range.FirstIndex && index <= range.LastIndex;
    }
}
