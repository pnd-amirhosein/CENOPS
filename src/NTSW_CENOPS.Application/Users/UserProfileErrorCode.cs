namespace NTSW_CENOPS.Application.Users;

public enum UserProfileErrorCode
{
    None = 0,
    UserNotFound,
    RequiredFields,
    DuplicateUsername,
    DuplicateDisplayName,
    InvalidUsername,
    DisplayNameTooLong,
    UsernameTooLong,
    UpdateFailed,
    InvalidBirthDate
}
