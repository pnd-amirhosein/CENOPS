namespace NTSW_CENOPS.Application.Administration;

public sealed class AdminUserModel
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
