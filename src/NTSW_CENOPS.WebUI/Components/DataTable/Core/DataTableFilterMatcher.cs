using System.Globalization;
using System.Collections;
using System.Reflection;
using NTSW_CENOPS.Components.DataTable.Models;

namespace NTSW_CENOPS.Components.DataTable.Core;

/// <summary>
/// Common table-filter contract: text is a case-insensitive substring search
/// (equivalent to LIKE '%value%'); boolean columns use exact true/false matching.
/// </summary>
public static class DataTableFilterMatcher
{
    public static bool Matches<T>(T item, FilterDefinition filter)
    {
        if (string.IsNullOrWhiteSpace(filter.PropertyName) || string.IsNullOrWhiteSpace(filter.Value))
            return true;

        var property = typeof(T).GetProperty(filter.PropertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is null || item is null)
            return false;

        var value = property.GetValue(item);
        if (value is null)
            return false;

        var search = filter.Value.Trim();
        if (value is bool boolean)
            return bool.TryParse(search, out var expected) && boolean == expected;

        if (value is IEnumerable values && value is not string)
            return values.Cast<object?>().Any(text => (text?.ToString() ?? string.Empty)
                .Contains(search, StringComparison.OrdinalIgnoreCase));

        return (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
            .Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}