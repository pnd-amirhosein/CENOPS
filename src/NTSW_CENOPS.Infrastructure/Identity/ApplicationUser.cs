using Microsoft.AspNetCore.Identity;

namespace NTSW_CENOPS.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<int>
{
    public required string DisplayName { get; set; }
    public bool IsDeleted { get; set; }
    /// <summary>Gregorian date at midnight; only presentation uses the Persian calendar.</summary>
    public DateTime? BirthDate { get; set; }
}
