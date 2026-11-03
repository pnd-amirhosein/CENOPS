namespace NTSW_CENOPS.Application.Administration;

public sealed class AdminRoleModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Accesses { get; set; } = new();
}
