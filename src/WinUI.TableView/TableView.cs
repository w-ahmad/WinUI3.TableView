using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations.Expressions;
using CommunityToolkit.WinUI.Collections;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;
using WinUI.TableView.Extensions;

namespace WinUI.TableView;
public partial class TableView : ListView
{
    private TableViewHeaderRow? _headerRow;
    private ScrollViewer _scrollViewer = null!;
    private bool _shouldThrowSelectionModeChangedException;

    public TableView()
    {
        DefaultStyleKey = typeof(TableView);

        CollectionView.Filter = Filter;
        base.ItemsSource = CollectionView;
        base.SelectionMode = SelectionMode;
        RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, OnBaseItemsSourceChanged);
        RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, OnBaseSelectionModeChanged);
        Loaded += OnLoaded;
        SelectionChanged += TableView_SelectionChanged;
    }

    private void TableView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!KeyBoardHelper.IsCtrlKeyDown())
        {
            SelectedCellRanges.Clear();
        }
        else
        {
            SelectedCellRanges.RemoveWhere(slots =>
            {
                slots.RemoveWhere(slot => SelectedRanges.Any(range => range.IsInRange(slot.Row)));
                return slots.Count == 0;
            });
        }

        SetCurrentCell(null);
        OnCellSelectionChanged();
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (element is TableViewRow row)
        {
            row.ApplyCellsSelectionState();

            if (CurrentCellSlot.HasValue)
            {
                row.ApplyCurrentCellState(CurrentCellSlot.Value);
            }
        }
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new TableViewRow { TableView = this };
    }

    protected override void OnPreviewKeyDown(KeyRoutedEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        var shiftKey = KeyBoardHelper.IsShiftKeyDown();
        var ctrlKey = KeyBoardHelper.IsCtrlKeyDown();
        var currentCell = CurrentCellSlot.HasValue ? GetCellFromSlot(CurrentCellSlot.Value) : default;

        if (HandleShortKeys(shiftKey, ctrlKey, e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key is VirtualKey.F2 && currentCell is not null && !IsEditing)
        {
            currentCell.PrepareForEdit();
            IsEditing = true;
            e.Handled = true;
        }
        else if (e.Key is VirtualKey.Escape && currentCell is not null && IsEditing)
        {
            currentCell?.SetElement();
            IsEditing = false;
            e.Handled = true;
        }
        else if (e.Key is VirtualKey.Space && currentCell is not null && CurrentCellSlot.HasValue && !IsEditing)
        {
            if (!currentCell.IsSelected)
            {
                SelectCells(CurrentCellSlot.Value, shiftKey, ctrlKey);
            }
            else
            {
                DeselectCell(CurrentCellSlot.Value);
            }
        }
        // Handle navigation keys
        else if (e.Key is VirtualKey.Tab or VirtualKey.Enter)
        {
            var isEditing = IsEditing;
            var newSlot = GetNextSlot(CurrentCellSlot, shiftKey, e.Key is VirtualKey.Enter);

            SelectCells(newSlot, false);

            if (isEditing && currentCell is not null)
            {
                currentCell = GetCellFromSlot(newSlot);
                currentCell.PrepareForEdit();
            }

            e.Handled = true;
        }
        else if ((e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)
                 && !IsEditing)
        {
            var row = CurrentCellSlot?.Row ?? 0;
            var column = CurrentCellSlot?.Column ?? 0;

            if (e.Key is VirtualKey.Left or VirtualKey.Right)
            {
                column = e.Key is VirtualKey.Left ? ctrlKey ? 0 : column - 1 : ctrlKey ? Columns.VisibleColumns.Count - 1 : column + 1;
            }
            else
            {
                row = e.Key == VirtualKey.Up ? ctrlKey ? 0 : row - 1 : ctrlKey ? Items.Count - 1 : row + 1;
            }

            var newSlot = new TableViewCellSlot(row, column);
            SelectCells(newSlot, shiftKey);
            e.Handled = true;
        }
    }

    private bool HandleShortKeys(bool shiftKey, bool ctrlKey, VirtualKey key)
    {
        if (key == VirtualKey.A && ctrlKey && !shiftKey)
        {
            SelectAll();
            return true;
        }
        else if (key == VirtualKey.A && ctrlKey && shiftKey)
        {
            DeselectAll();
            return true;
        }
        else if (key == VirtualKey.C && ctrlKey)
        {
            CopyToClipboardInternal(shiftKey);
            return true;
        }

        return false;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _headerRow = GetTemplateChild("HeaderRow") as TableViewHeaderRow;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (GetTemplateChild("ScrollViewer") is not ScrollViewer scrollViewer)
        {
            return;
        }

        _scrollViewer = scrollViewer;
        Canvas.SetZIndex(ItemsPanelRoot, -1);

        var scrollProperties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);
        var compositor = scrollProperties.Compositor;
        var scrollPropSet = scrollProperties.GetSpecializedReference<ManipulationPropertySetReferenceNode>();
        var itemsPanelVisual = ElementCompositionPreview.GetElementVisual(ItemsPanelRoot);
        var contentClip = compositor.CreateInsetClip();
        var expressionClipAnimation = ExpressionFunctions.Max(-scrollPropSet.Translation.Y, 0);

        itemsPanelVisual.Clip = contentClip;
        contentClip.TopInset = (float)Math.Max(-scrollViewer.VerticalOffset, 0);
        contentClip.StartAnimation("TopInset", expressionClipAnimation);

        UpdateVerticalScrollBarMargin();
    }

    private TableViewCellSlot GetNextSlot(TableViewCellSlot? currentSlot, bool isShiftKeyDown, bool isEnterKey)
    {
        var rows = Items.Count;
        var columns = Columns.VisibleColumns.Count;
        var currentRow = currentSlot?.Row ?? 0;
        var currentColumn = currentSlot?.Column ?? 0;
        var nextRow = currentRow;
        var nextColumn = currentColumn;

        if (isEnterKey)
        {
            nextRow += isShiftKeyDown ? -1 : 1;
            if (nextRow < 0)
            {
                nextRow = rows - 1;
                nextColumn = (nextColumn - 1 + columns) % columns;
            }
            else if (nextRow >= rows)
            {
                nextRow = 0;
                nextColumn = (nextColumn + 1) % columns;
            }
        }
        else
        {
            nextColumn += isShiftKeyDown ? -1 : 1;
            if (nextColumn < 0)
            {
                nextColumn = columns - 1;
                nextRow = (nextRow - 1 + rows) % rows;
            }
            else if (nextColumn >= columns)
            {
                nextColumn = 0;
                nextRow = (nextRow + 1) % rows;
            }
        }

        return new TableViewCellSlot(nextRow, nextColumn);
    }

    private bool Filter(object obj)
    {
        return ActiveFilters.All(item => item.Value(obj));
    }

    internal void CopyToClipboardInternal(bool includeHeaders)
    {
        var args = new TableViewCopyToClipboardEventArgs(includeHeaders);
        OnCopyToClipboard(args);

        if (args.Handled)
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(GetSelectedContent(includeHeaders));
        Clipboard.SetContent(package);
    }

    protected virtual void OnCopyToClipboard(TableViewCopyToClipboardEventArgs args)
    {
        CopyToClipboard?.Invoke(this, args);
    }

    public string GetSelectedContent(bool includeHeaders, char separator = '\t')
    {
        var slots = Enumerable.Empty<TableViewCellSlot>();

        if (SelectedItems.Any() || SelectedCells.Any())
        {
            slots = SelectedRanges.SelectMany(x => Enumerable.Range(x.FirstIndex, (int)x.Length))
                                  .SelectMany(r => Enumerable.Range(0, Columns.VisibleColumns.Count)
                                  .Select(c => new TableViewCellSlot(r, c)))
                                  .Concat(SelectedCells)
                                  .OrderBy(x => x.Row)
                                  .ThenByDescending(x => x.Column);
        }
        else if (CurrentCellSlot.HasValue)
        {
            slots = new[] { CurrentCellSlot.Value };
        }

        return GetCellsContent(slots, includeHeaders, separator);
    }

    public string GetAllContent(bool includeHeaders, char separator = '\t')
    {
        var slots = Enumerable.Range(0, Items.Count)
                              .SelectMany(r => Enumerable.Range(0, Columns.VisibleColumns.Count)
                              .Select(c => new TableViewCellSlot(r, c)))
                              .OrderBy(x => x.Row)
                              .ThenByDescending(x => x.Column);

        return GetCellsContent(slots, includeHeaders, separator);
    }

    private string GetCellsContent(IEnumerable<TableViewCellSlot> slots, bool includeHeaders, char separator)
    {
        if (!slots.Any())
        {
            return string.Empty;
        }

        var minRow = slots.Select(x => x.Row).Min();
        var maxRow = slots.Select(x => x.Row).Max();
        var minColumn = slots.Select(x => x.Column).Min();
        var maxColumn = slots.Select(x => x.Column).Max();

        var stringBuilder = new StringBuilder();
        var properties = new Dictionary<string, (PropertyInfo, object?)[]>();

        if (includeHeaders)
        {
            stringBuilder.AppendLine(GetHeadersContent(separator, minColumn, maxColumn));
        }

        for (var row = minRow; row <= maxRow; row++)
        {
            var item = Items[row];
            var type = ItemsSource?.GetType() is { } listType && listType.IsGenericType ? listType.GetGenericArguments()[0] : item?.GetType();

            for (var col = minColumn; col <= maxColumn; col++)
            {
                if (Columns.VisibleColumns[col] is not TableViewBoundColumn column ||
                   !slots.Contains(new TableViewCellSlot(row, col)))
                {
                    stringBuilder.Append('\t');
                    continue;
                }

                var property = column.Binding.Path.Path;
                if (!properties.TryGetValue(property, out var pis))
                {
                    stringBuilder.Append($"{item.GetValue(type, property, out pis)}{separator}");
                    properties.Add(property, pis);
                }
                else
                {
                    stringBuilder.Append($"{item.GetValue(pis)}{separator}");
                }
            }

            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            stringBuilder.Append('\n');
        }

        return stringBuilder.ToString();
    }

    private string GetHeadersContent(char separator, int minColumn, int maxColumn)
    {
        var stringBuilder = new StringBuilder();
        for (var col = minColumn; col <= maxColumn; col++)
        {
            var column = Columns.VisibleColumns[col];
            stringBuilder.Append($"{column.Header}{separator}");
        }

        return stringBuilder.ToString();
    }

    private void GenerateColumns()
    {
        var itemsSourceType = ItemsSource?.GetType();
        if (itemsSourceType?.IsGenericType == true)
        {
            var dataType = itemsSourceType.GenericTypeArguments[0];
            foreach (var propertyInfo in dataType.GetProperties())
            {
                var displayAttribute = propertyInfo.GetCustomAttributes().OfType<DisplayAttribute>().FirstOrDefault();
                var autoGenerateField = displayAttribute?.GetAutoGenerateField();
                if (autoGenerateField == false)
                {
                    continue;
                }

                var header = displayAttribute?.GetShortName() ?? propertyInfo.Name;
                var canFilter = displayAttribute?.GetAutoGenerateFilter() ?? true;
                var columnArgs = GenerateColumn(propertyInfo.PropertyType, propertyInfo.Name, header, canFilter);
                OnAutoGeneratingColumn(columnArgs);

                if (!columnArgs.Cancel && columnArgs.Column is not null)
                {
                    Columns.Insert(displayAttribute?.GetOrder() ?? Columns.Count, columnArgs.Column);
                }
            }
        }
    }

    private static TableViewAutoGeneratingColumnEventArgs GenerateColumn(Type propertyType, string propertyName, string header, bool canFilter)
    {
        var newColumn = GetTableViewColumnFromType(propertyType);
        newColumn.Header = header;
        newColumn.CanFilter = canFilter;
        newColumn.IsAutoGenerated = true;
        newColumn.Binding = new Binding { Path = new PropertyPath(propertyName), Mode = BindingMode.TwoWay };
        return new TableViewAutoGeneratingColumnEventArgs(propertyName, propertyType, newColumn);
    }

    protected virtual void OnAutoGeneratingColumn(TableViewAutoGeneratingColumnEventArgs e)
    {
        AutoGeneratingColumn?.Invoke(this, e);
    }

    private static TableViewBoundColumn GetTableViewColumnFromType(Type type)
    {
        if (type == typeof(bool) || type == typeof(bool?))
        {
            return new TableViewCheckBoxColumn();
        }
        else if (type == typeof(int) || type == typeof(int?) ||
                 type == typeof(double) || type == typeof(double?) ||
                 type == typeof(float) || type == typeof(float?) ||
                 type == typeof(decimal) || type == typeof(decimal?))
        {
            return new TableViewNumberColumn();
        }

        return new TableViewTextColumn();
    }

    private void OnItemsSourceChanged(DependencyPropertyChangedEventArgs e)
    {
        ((AdvancedCollectionView)CollectionView).Source = null!;

        if (e.NewValue is IList source)
        {
            if (AutoGenerateColumns)
            {
                RemoveAutoGeneratedColumns();
                GenerateColumns();
            }

            ((AdvancedCollectionView)CollectionView).Source = source;
        }
    }

    private void RemoveAutoGeneratedColumns()
    {
        while (Columns.Any(x => x.IsAutoGenerated))
        {
            var autoGeneratedColumn = Columns.First(x => x.IsAutoGenerated);
            Columns.Remove(autoGeneratedColumn);
        }
    }

    internal async void ExportSelectedToCSV()
    {
        var args = new TableViewExportContentEventArgs();
        OnExportSelectedContent(args);

        if (args.Handled)
        {
            return;
        }

        try
        {
            var hWnd = Win32Interop.GetWindowFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);
            if (await GetStorageFile(hWnd) is not { } file)
            {
                return;
            }

            var content = GetSelectedContent(true, ',');
            using var stream = await file.OpenStreamForWriteAsync();
            stream.SetLength(0);

            using var tw = new StreamWriter(stream);
            await tw.WriteAsync(content);
        }
        catch { }
    }

    protected virtual void OnExportSelectedContent(TableViewExportContentEventArgs args)
    {
        ExportSelectedContent?.Invoke(this, args);
    }

    internal async void ExportAllToCSV()
    {
        var args = new TableViewExportContentEventArgs();
        OnExportAllContent(args);

        if (args.Handled)
        {
            return;
        }

        try
        {
            var hWnd = Win32Interop.GetWindowFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);
            if (await GetStorageFile(hWnd) is not { } file)
            {
                return;
            }

            var content = GetAllContent(true, ',');
            using var stream = await file.OpenStreamForWriteAsync();
            stream.SetLength(0);

            using var tw = new StreamWriter(stream);
            await tw.WriteAsync(content);
            await tw.FlushAsync(); // needed?
        }
        catch { }
    }

    protected virtual void OnExportAllContent(TableViewExportContentEventArgs args)
    {
        ExportAllContent?.Invoke(this, args);
    }

    private static async Task<StorageFile> GetStorageFile(IntPtr hWnd)
    {
        var savePicker = new FileSavePicker();
        InitializeWithWindow.Initialize(savePicker, hWnd);
        savePicker.FileTypeChoices.Add("CSV (Comma delimited)", new List<string>() { ".csv" });
        return await savePicker.PickSaveFileAsync();
    }

    private void UpdateVerticalScrollBarMargin()
    {
        if (GetTemplateChild("ScrollViewer") is ScrollViewer scrollViewer)
        {
            var verticalScrollBar = scrollViewer.FindDescendant<ScrollBar>(x => x.Name == "VerticalScrollBar");
            if (verticalScrollBar is not null)
            {
                verticalScrollBar.Margin = new Thickness(0, HeaderRowHeight, 0, 0);
            }
        }
    }

    internal void ClearSorting()
    {
        DeselectAll();
        CollectionView.SortDescriptions.Clear();

        foreach (var header in Columns.Select(x => x.HeaderControl))
        {
            if (header is not null)
            {
                header.SortDirection = null;
            }
        }
    }

    internal void ClearFilters()
    {
        DeselectAll();
        ActiveFilters.Clear();
        CollectionView.RefreshFilter();

        foreach (var header in Columns.Select(x => x.HeaderControl))
        {
            if (header is not null)
            {
                header.IsFiltered = false;
            }
        }
    }

    private bool IsValidSlot(TableViewCellSlot slot)
    {
        return slot.Row >= 0 && slot.Column >= 0 && slot.Row < Items.Count && slot.Column < Columns.VisibleColumns.Count;
    }

    internal new void SelectAll()
    {
        if (IsEditing)
        {
            return;
        }

        if (SelectionUnit is TableViewSelectionUnit.Cell)
        {
            SelectAllCells();
            SetCurrentCell(null);
        }
        else
        {
            switch (SelectionMode)
            {
                case ListViewSelectionMode.Single:
                    SelectedItem = Items.FirstOrDefault();
                    break;
                case ListViewSelectionMode.Multiple:
                case ListViewSelectionMode.Extended:
                    SelectRange(new ItemIndexRange(0, (uint)Items.Count));
                    break;
            }
        }
    }

    private void SelectAllCells()
    {
        switch (SelectionMode)
        {
            case ListViewSelectionMode.Single:
                if (Items.Count > 0 && Columns.VisibleColumns.Count > 0)
                {
                    SelectedCellRanges.Clear();
                    SelectedCellRanges.Add(new() { new TableViewCellSlot(0, 0) });
                }
                break;
            case ListViewSelectionMode.Multiple:
            case ListViewSelectionMode.Extended:
                SelectedCellRanges.Clear();
                var selectionRange = new HashSet<TableViewCellSlot>();

                for (var row = 0; row < Items.Count; row++)
                {
                    for (var column = 0; column < Columns.VisibleColumns.Count; column++)
                    {
                        selectionRange.Add(new TableViewCellSlot(row, column));
                    }
                }
                SelectedCellRanges.Add(selectionRange);
                break;
        }

        OnCellSelectionChanged();
    }

    internal void DeselectAll()
    {
        DeselectAllItems();
        DeselectAllCells();
    }

    private void DeselectAllItems()
    {
        switch (SelectionMode)
        {
            case ListViewSelectionMode.Single:
                SelectedItem = null;
                break;
            case ListViewSelectionMode.Multiple:
            case ListViewSelectionMode.Extended:
                DeselectRange(new ItemIndexRange(0, (uint)Items.Count));
                break;
        }
    }

    void DeselectAllCells()
    {
        SelectedCellRanges.Clear();
        OnCellSelectionChanged();
        SetCurrentCell(null);
    }

    internal async void SelectCells(TableViewCellSlot slot, bool shiftKey, bool ctrlKey = false)
    {
        if (!IsValidSlot(slot))
        {
            return;
        }

        if (SelectionMode != ListViewSelectionMode.None && SelectionUnit != TableViewSelectionUnit.Row)
        {
            ctrlKey = ctrlKey || SelectionMode is ListViewSelectionMode.Multiple;
            if (!ctrlKey || !(SelectionMode is ListViewSelectionMode.Multiple or ListViewSelectionMode.Extended))
            {
                if (SelectedItems.Count > 0)
                {
                    DeselectAllItems();
                }

                if (SelectedCells.Count > 0)
                {
                    SelectedCellRanges.Clear();
                }
            }

            var selectionRange = (SelectionStartCellSlot is null ? null : SelectedCellRanges.LastOrDefault(x => SelectionStartCellSlot.HasValue && x.Contains(SelectionStartCellSlot.Value))) ?? new HashSet<TableViewCellSlot>();
            SelectedCellRanges.Remove(selectionRange);
            selectionRange.Clear();
            SelectionStartCellSlot ??= CurrentCellSlot;
            SelectionStartCellSlot ??= slot;
            if (shiftKey && SelectionMode is ListViewSelectionMode.Multiple or ListViewSelectionMode.Extended)
            {
                var currentSlot = SelectionStartCellSlot.Value;
                var startRow = Math.Min(slot.Row, currentSlot.Row);
                var endRow = Math.Max(slot.Row, currentSlot.Row);
                var startCol = Math.Min(slot.Column, currentSlot.Column);
                var endCol = Math.Max(slot.Column, currentSlot.Column);
                for (var row = startRow; row <= endRow; row++)
                {
                    for (var column = startCol; column <= endCol; column++)
                    {
                        var nextSlot = new TableViewCellSlot(row, column);
                        selectionRange.Add(nextSlot);
                        if (SelectedCellRanges.LastOrDefault(x => x.Contains(nextSlot)) is { } range)
                        {
                            range.Remove(nextSlot);
                        }
                    }
                }
            }
            else
            {
                SelectionStartCellSlot = slot;
                selectionRange.Add(slot);

                if (SelectedCellRanges.LastOrDefault(x => x.Contains(slot)) is { } range)
                {
                    range.Remove(slot);
                }
            }

            SetCurrentCell((await ScrollCellIntoView(slot)).Slot);
            SelectedCellRanges.Add(selectionRange);
            OnCellSelectionChanged();
        }
        else
        {
            SetCurrentCell((await ScrollCellIntoView(slot)).Slot);
        }
    }

    internal void DeselectCell(TableViewCellSlot slot)
    {
        var selectionRange = SelectedCellRanges.LastOrDefault(x => x.Contains(slot));
        selectionRange?.Remove(slot);

        if (selectionRange?.Count == 0)
        {
            SelectedCellRanges.Remove(selectionRange);
        }

        SetCurrentCell(slot);
        OnCellSelectionChanged();
    }

    internal void SetCurrentCell(TableViewCellSlot? slot)
    {
        if (slot == CurrentCellSlot)
        {
            return;
        }

        var oldSlot = CurrentCellSlot;
        var currentCell = oldSlot.HasValue ? GetCellFromSlot(oldSlot.Value) : default;
        currentCell?.SetElement();
        CurrentCellSlot = slot;
        CurrentCellChanged?.Invoke(this, new TableViewCurrentCellChangedEventArgs(oldSlot, slot));
    }

    void OnCellSelectionChanged()
    {
        var oldSelection = SelectedCells;
        SelectedCells = new HashSet<TableViewCellSlot>(SelectedCellRanges.SelectMany(x => x));
        SelectedCellsChanged?.Invoke(this, new TableViewCellSelectionChangedEvenArgs(oldSelection, SelectedCells));
    }

    internal async Task<TableViewCell> ScrollCellIntoView(TableViewCellSlot slot)
    {
        var row = await ScrollRowIntoView(slot.Row);
        var (start, end) = GetColumnsInDisplay();
        var xOffset = 0d;
        var yOffset = _scrollViewer.VerticalOffset;

        if (slot.Column < start)
        {
            for (var i = 0; i < slot.Column; i++)
            {
                xOffset += Columns.VisibleColumns[i].ActualWidth;
            }
        }
        else if (slot.Column > end)
        {
            for (var i = 0; i <= slot.Column; i++)
            {
                xOffset += Columns.VisibleColumns[i].ActualWidth;
            }

            var change = xOffset - _scrollViewer.HorizontalOffset - (_scrollViewer.ViewportWidth - 16);
            xOffset = _scrollViewer.HorizontalOffset + change;
        }
        else if (row is not null)
        {
            return row.Cells.ElementAt(slot.Column);
        }

        var tcs = new TaskCompletionSource<object?>();

        void ViewChanged(object? _, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate)
            {
                return;
            }

            tcs.TrySetResult(result: default);
        }

        try
        {
            _scrollViewer.ViewChanged += ViewChanged;
            _scrollViewer.ChangeView(xOffset, yOffset, null, true);
            _scrollViewer.ScrollToHorizontalOffset(xOffset);
            await tcs.Task;
        }
        finally
        {
            _scrollViewer.ViewChanged -= ViewChanged;
        }

        return row?.Cells.ElementAt(slot.Column)!;
    }

    async Task<TableViewRow?> ScrollRowIntoView(int index)
    {
        var item = Items[index];
        index = Items.IndexOf(item); // if the ItemsSource has duplicate items in it. ScrollIntoView will only bring first index of item.
        ScrollIntoView(item);

        var tries = 0;
        while (tries < 10)
        {
            if (ContainerFromIndex(index) is TableViewRow row)
            {
                var transform = row.TransformToVisual(_scrollViewer);
                var positionInScrollViewer = transform.TransformPoint(new Point(0, 0));
                if ((index == 0 && _scrollViewer.VerticalOffset > 0) || (index > 0 && positionInScrollViewer.Y <= row.ActualHeight))
                {
                    var xOffset = _scrollViewer.HorizontalOffset;
                    var yOffset = index == 0 ? 0d : _scrollViewer.VerticalOffset - row.ActualHeight + positionInScrollViewer.Y + 8;
                    var tcs = new TaskCompletionSource<object?>();

                    try
                    {
                        _scrollViewer.ViewChanged += ViewChanged;
                        _scrollViewer.ChangeView(xOffset, yOffset, null, true);
                        await tcs.Task;
                    }
                    finally
                    {
                        _scrollViewer.ViewChanged -= ViewChanged;
                    }

                    void ViewChanged(object? _, ScrollViewerViewChangedEventArgs e)
                    {
                        if (e.IsIntermediate)
                        {
                            return;
                        }

                        tcs.TrySetResult(result: default);
                    }
                }

                row.Focus(FocusState.Programmatic);
                return row;
            }

            tries++;
            await Task.Delay(1); // let the animation complete
        }

        return default;
    }

    internal TableViewCell GetCellFromSlot(TableViewCellSlot slot)
    {
        return ContainerFromIndex(slot.Row) is TableViewRow row ? row.Cells[slot.Column] : default!;
    }

    private (int start, int end) GetColumnsInDisplay()
    {
        var start = -1;
        var end = -1;
        var width = 0d;
        foreach (var column in Columns.VisibleColumns)
        {
            if (width >= _scrollViewer.HorizontalOffset && width + column.ActualWidth <= _scrollViewer.HorizontalOffset + _scrollViewer.ViewportWidth)
            {
                if (start == -1)
                {
                    start = end = Columns.VisibleColumns.IndexOf(column);
                }
                else
                {
                    end = Columns.VisibleColumns.IndexOf(column);
                }
            }

            width += column.ActualWidth;
        }

        return (start, end);
    }

    void UpdateBaseSelectionMode()
    {
        _shouldThrowSelectionModeChangedException = true;
        base.SelectionMode = SelectionUnit is TableViewSelectionUnit.Cell ? ListViewSelectionMode.None : SelectionMode;
        _shouldThrowSelectionModeChangedException = false;
    }

    public event EventHandler<TableViewAutoGeneratingColumnEventArgs>? AutoGeneratingColumn;
    public event EventHandler<TableViewExportContentEventArgs>? ExportAllContent;
    public event EventHandler<TableViewExportContentEventArgs>? ExportSelectedContent;
    public event EventHandler<TableViewCopyToClipboardEventArgs>? CopyToClipboard;
    internal event EventHandler<TableViewCellSelectionChangedEvenArgs>? SelectedCellsChanged;
    internal event EventHandler<TableViewCurrentCellChangedEventArgs>? CurrentCellChanged;
}
