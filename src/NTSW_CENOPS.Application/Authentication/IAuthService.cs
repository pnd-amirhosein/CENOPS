namespace NTSW_CENOPS.Application.Authentication;

public interface IAuthService
{
    Task<AuthServiceResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthServiceResult> SignupAsync(
        string displayName,
        string username,
        string password,
        string confirmPassword,
        CancellationToken cancellationToken = default);

    Task LogoutAsync();
}
