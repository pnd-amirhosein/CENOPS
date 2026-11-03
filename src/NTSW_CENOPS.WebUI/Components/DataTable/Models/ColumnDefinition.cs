using Microsoft.AspNetCore.Components;

namespace NTSW_CENOPS.Components.DataTable.Models;

public class ColumnDefinition<T>
{
    public string Title { get; set; } = "";
    public string PropertyName { get; set; } = "";

    public ColumnType Type { get; set; } = ColumnType.Text;

    public Func<T, object?> Value { get; set; } = default!;

    public bool IsReadOnly { get; set; }

    // Optional select editor for finite values, e.g. monitoring data sources.
    public IReadOnlyList<string>? Options { get; set; }
    // Select option value stays stable while the user sees a friendly configured label.
    public IReadOnlyDictionary<string, string>? OptionLabels { get; set; }
    public Func<T, string?>? DisplayValue { get; set; }

    public string? width { get; set; }
}