using Microsoft.AspNetCore.Identity;

namespace NTSW_CENOPS.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<int>
{
    public bool IsDeleted { get; set; }
}
