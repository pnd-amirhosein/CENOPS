namespace NTSW_CENOPS.Application.Authentication;

public enum PasswordChangeErrorCode
{
    None = 0,
    RequiredFields,
    PasswordMismatch,
    CurrentPasswordInvalid,
    WeakPassword,
    UserNotFound,
    ChangeFailed
}
