using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WinUI.TableView;

public abstract class TableViewBoundColumn : TableViewColumn
{
    private Binding _binding = new();

    public virtual Binding Binding
    {
        get => _binding;
        set
        {
            _binding = value;
            if (_binding is not null)
            {
                _binding.Mode = BindingMode.TwoWay;
            }
        }
    }

    public bool CanSort
    {
        get => (bool)GetValue(CanSortProperty);
        set => SetValue(CanSortProperty, value);
    }

    public bool CanFilter
    {
        get => (bool)GetValue(CanFilterProperty);
        set => SetValue(CanFilterProperty, value);
    }

    public static readonly DependencyProperty CanSortProperty = DependencyProperty.Register(nameof(CanSort), typeof(bool), typeof(TableViewBoundColumn), new PropertyMetadata(true));
    public static readonly DependencyProperty CanFilterProperty = DependencyProperty.Register(nameof(CanFilter), typeof(bool), typeof(TableViewBoundColumn), new PropertyMetadata(true));
}
