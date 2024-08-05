using System;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Windows.Foundation;

namespace WinUI.TableView;

[TemplateVisualState(Name = VisualStates.StateNormal, GroupName = VisualStates.GroupCommon)]
[TemplateVisualState(Name = VisualStates.StatePointerOver, GroupName = VisualStates.GroupCommon)]
[TemplateVisualState(Name = VisualStates.StateRegular, GroupName = VisualStates.GroupCurrent)]
[TemplateVisualState(Name = VisualStates.StateCurrent, GroupName = VisualStates.GroupCurrent)]
[TemplateVisualState(Name = VisualStates.StateSelected, GroupName = VisualStates.GroupSelection)]
[TemplateVisualState(Name = VisualStates.StateUnselected, GroupName = VisualStates.GroupSelection)]
public class TableViewCell : ContentControl
{
    #region [My changes - Aug 3, 2024]
    static bool SingleClickEditing { get; set; } = false;
    #endregion

    public TableViewCell()
    {
        DefaultStyleKey = typeof(TableViewCell);
        Loaded += OnLoaded;
    }

    #region [Private]
    internal TableViewCellSlot Slot => new(Row.Index, Index);
    internal int Index { get; set; }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplySelectionState();
        #region [My changes - Aug 3, 2024]
        if (TableView is not null)
        {   // Detect editing mode set from TableView XAML.
            SingleClickEditing = TableView.SingleClickEditing;
        }
        #endregion
    }

    void SetEditingElement()
    {
        Content = Column.GenerateEditingElement();
        if (TableView is not null)
        {
            TableView.IsEditing = true;
        }
    }

    internal void ApplySelectionState()
    {
        var stateName = IsSelected ? VisualStates.StateSelected : VisualStates.StateUnselected;
        VisualStates.GoToState(this, false, stateName);
    }

    internal void ApplyCurrentCellState()
    {
        var stateName = IsCurrent ? VisualStates.StateCurrent : VisualStates.StateRegular;
        VisualStates.GoToState(this, false, stateName);
    }

    static void OnColumnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableViewCell cell)
        {
            if (cell.TableView?.IsEditing == true)
                cell.SetEditingElement();
            else
                cell.SetElement();
        }
    }

    internal async void PrepareForEdit()
    {
        SetEditingElement();
        await Task.Delay(20);
        if ((Content ?? ContentTemplateRoot) is UIElement editingElement)
        {
            editingElement.Focus(FocusState.Programmatic);
            if (editingElement is TextBox tb && !string.IsNullOrEmpty(tb.Text))
                tb.SelectAll();
        }
    }

    internal void SetElement()
    {
        Content = Column.GenerateElement();
    }
    #endregion

    #region [Protected]
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);

        if ((Content ?? ContentTemplateRoot) is FrameworkElement element)
        {
            var contentWidth = Column.ActualWidth;
            contentWidth -= element.Margin.Left;
            contentWidth -= element.Margin.Right;
            contentWidth -= Padding.Left;
            contentWidth -= Padding.Right;
            contentWidth -= BorderThickness.Left;
            contentWidth -= BorderThickness.Right;

            element.MaxWidth = contentWidth;

            if (Column is not null)
            {
                var desiredWidth = element.DesiredSize.Width;
                desiredWidth += Padding.Left;
                desiredWidth += Padding.Right;
                desiredWidth += BorderThickness.Left;
                desiredWidth += BorderThickness.Right;

                Column.DesiredWidth = Math.Max(Column.DesiredWidth, desiredWidth);
            }
        }

        return size;
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);

        VisualStates.GoToState(this, false, VisualStates.StatePointerOver);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        VisualStates.GoToState(this, false, VisualStates.StateNormal);
    }

    protected override void OnTapped(TappedRoutedEventArgs e)
    {
        base.OnTapped(e);

        if (TableView.IsEditing && TableView.CurrentCellSlot == Slot)
        {
            return;
        }

        var shiftKey = KeyBoardHelper.IsShiftKeyDown();
        var ctrlKey = KeyBoardHelper.IsCtrlKeyDown();

        if (IsSelected && (ctrlKey || TableView.SelectionMode is ListViewSelectionMode.Multiple) && !shiftKey)
        {
            TableView.DeselectCell(Slot);
        }
        else
        {
            TableView.IsEditing = false;
            TableView.SelectCells(Slot, shiftKey, ctrlKey);
        }

        Focus(FocusState.Programmatic);

        #region [My changes - Aug 3, 2024]
        if (!TableView.IsEditing && SingleClickEditing)
        {
            OnDoubleTapped(new DoubleTappedRoutedEventArgs());
        }
        #endregion
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!KeyBoardHelper.IsShiftKeyDown())
        {
            TableView.SelectionStartCellSlot = Slot;
        }
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!KeyBoardHelper.IsShiftKeyDown())
        {
            TableView.SelectionStartCellSlot = null;
        }
        e.Handled = TableView.SelectionUnit != TableViewSelectionUnit.Row;
    }

    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed && !TableView.IsEditing)
        {
            var ctrlKey = KeyBoardHelper.IsCtrlKeyDown();
            TableView.SelectCells(Slot, true, ctrlKey);
        }
    }

    protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e)
    {
        if (!IsReadOnly)
        {
            PrepareForEdit();
            TableView.IsEditing = true;
        }
    }
    #endregion

    #region [Public]
    public bool IsReadOnly => TableView.IsReadOnly || Column is TableViewTemplateColumn { EditingTemplate: null } or { IsReadOnly: true };
    public bool IsSelected => TableView.SelectedCells.Contains(Slot);
    public bool IsCurrent => TableView.CurrentCellSlot == Slot;

    public TableViewColumn Column
    {
        get => (TableViewColumn)GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    public TableViewRow Row
    {
        get => (TableViewRow)GetValue(TableViewRowProperty);
        set => SetValue(TableViewRowProperty, value);
    }

    public TableView TableView
    {
        get => (TableView)GetValue(TableViewProperty);
        set => SetValue(TableViewProperty, value);
    }

    public static readonly DependencyProperty ColumnProperty = DependencyProperty.Register(nameof(Column), typeof(TableViewColumn), typeof(TableViewCell), new PropertyMetadata(default, OnColumnChanged));
    public static readonly DependencyProperty TableViewRowProperty = DependencyProperty.Register(nameof(Row), typeof(TableViewRow), typeof(TableViewCell), new PropertyMetadata(default));
    public static readonly DependencyProperty TableViewProperty = DependencyProperty.Register(nameof(TableView), typeof(TableView), typeof(TableViewCell), new PropertyMetadata(default));
    #endregion
}
