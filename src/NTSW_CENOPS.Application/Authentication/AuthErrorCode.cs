namespace NTSW_CENOPS.Application.Authentication;

public enum AuthErrorCode
{
    None = 0,
    RequiredFields,
    InvalidCredentials,
    LockedOut,
    DuplicateUsername,
    DuplicateDisplayName,
    InvalidUsername,
    DisplayNameTooLong,
    UsernameTooLong,
    WeakPassword,
    PasswordMismatch,
    RoleCreationFailed,
    SignupFailed
}
