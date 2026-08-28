using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Partcounter.Views;

public partial class AlsIntegrationView : UserControl
{
    private static readonly HashSet<string> ReadOnlyMappingProperties = new(StringComparer.Ordinal)
    {
        "Required",
        "TargetField",
        "Description",
        "Example"
    };

    public AlsIntegrationView()
    {
        InitializeComponent();
        ApplyReadOnlyMappingBindingHotfix(this);
    }

    private static void ApplyReadOnlyMappingBindingHotfix(DependencyObject root)
    {
        foreach (var dataGrid in FindLogicalDescendants<DataGrid>(root))
        {
            foreach (var column in dataGrid.Columns)
            {
                switch (column)
                {
                    case DataGridCheckBoxColumn checkBoxColumn:
                        checkBoxColumn.Binding = ForceOneWayIfReadOnly(checkBoxColumn.Binding);
                        break;
                    case DataGridTextColumn textColumn:
                        textColumn.Binding = ForceOneWayIfReadOnly(textColumn.Binding);
                        break;
                }
            }
        }
    }

    private static BindingBase? ForceOneWayIfReadOnly(BindingBase? bindingBase)
    {
        if (bindingBase is not Binding binding ||
            binding.Path?.Path is not string path ||
            !ReadOnlyMappingProperties.Contains(path))
            return bindingBase;

        return new Binding(path)
        {
            Mode = BindingMode.OneWay,
            StringFormat = binding.StringFormat,
            TargetNullValue = binding.TargetNullValue,
            FallbackValue = binding.FallbackValue
        };
    }

    private static IEnumerable<T> FindLogicalDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject dependencyObject)
                continue;

            if (dependencyObject is T match)
                yield return match;

            foreach (var nested in FindLogicalDescendants<T>(dependencyObject))
                yield return nested;
        }
    }
}
