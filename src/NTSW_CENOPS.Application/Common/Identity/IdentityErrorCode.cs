namespace NTSW_CENOPS.Application.Common.Identity;

public enum IdentityErrorCode
{
    None = 0,
    InvalidCredentials,
    LockedOut,
    DuplicateUsername,
    DuplicateDisplayName,
    InvalidUsername,
    WeakPassword,
    RoleCreationFailed,
    UserNotFound,
    CurrentPasswordInvalid,
    OperationFailed
}
