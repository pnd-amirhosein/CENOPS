namespace NTSW_CENOPS.Domain.Contacts;

public class Contact
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string CellPhone { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}
