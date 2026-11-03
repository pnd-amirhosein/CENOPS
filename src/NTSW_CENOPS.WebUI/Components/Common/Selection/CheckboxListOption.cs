namespace NTSW_CENOPS.Components.Common.Selection;

public sealed record CheckboxListOption(int Id, string Title, string? Subtitle = null, string? Description = null);
public sealed record CheckboxListChange(int Id, bool IsChecked);
