namespace NTSW_CENOPS.Application.Access;

public sealed class AccessModel
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
