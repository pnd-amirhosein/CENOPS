using System.Globalization;
using NTSW_CENOPS.Application.Authentication;
using NTSW_CENOPS.Application.Users;
using Microsoft.AspNetCore.Antiforgery;

namespace NTSW_CENOPS.Endpoints;

public static class AuthEndpointExtensions
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/login", LoginAsync).AllowAnonymous();
        endpoints.MapPost("/auth/signup", SignupAsync).AllowAnonymous();
        endpoints.MapPost("/auth/logout", LogoutAsync).RequireAuthorization();
        endpoints.MapPost("/auth/profile", UpdateProfileAsync).RequireAuthorization();
        endpoints.MapPost("/auth/change-password", ChangePasswordAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthService authService)
    {
        await antiforgery.ValidateRequestAsync(context);

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var username = form["username"].ToString().Trim();
        var password = form["password"].ToString();
        var returnUrl = form["returnUrl"].ToString();

        var result = await authService.LoginAsync(
            username,
            password,
            context.RequestAborted);

        if (result.Succeeded)
        {
            return Results.LocalRedirect(IsLocalUrl(returnUrl) ? returnUrl : "/dashboard");
        }

        return Results.LocalRedirect(
            BuildLoginErrorUrl(ToLoginErrorCode(result.ErrorCode), username, returnUrl));
    }

    private static async Task<IResult> SignupAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthService authService)
    {
        await antiforgery.ValidateRequestAsync(context);

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var displayName = form["displayName"].ToString().Trim();
        var username = form["username"].ToString().Trim();
        var password = form["password"].ToString();
        var confirmPassword = form["confirmPassword"].ToString();

        var result = await authService.SignupAsync(
            displayName,
            username,
            password,
            confirmPassword,
            context.RequestAborted);

        if (result.Succeeded)
        {
            return Results.LocalRedirect(
                $"/?registered=1&username={Uri.EscapeDataString(username)}");
        }

        return Results.LocalRedirect(
            BuildSignupErrorUrl(
                ToSignupErrorCode(result.ErrorCode),
                displayName,
                username,
                result.ErrorDetails));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthService authService)
    {
        await antiforgery.ValidateRequestAsync(context);
        await authService.LogoutAsync();
        return Results.LocalRedirect("/");
    }

    private static async Task<IResult> UpdateProfileAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IUserProfileService userProfileService)
    {
        await antiforgery.ValidateRequestAsync(context);

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var displayName = form["displayName"].ToString().Trim();
        var username = form["username"].ToString().Trim();
        var returnUrl = form["returnUrl"].ToString();
        var safeReturnUrl = IsLocalUrl(returnUrl) ? returnUrl : "/dashboard";
        var rawBirthDate = form["birthDate"].ToString();
        DateTime? birthDate = null;
        if (!string.IsNullOrWhiteSpace(rawBirthDate))
        {
            if (!DateTime.TryParseExact(rawBirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                return Results.LocalRedirect(BuildProfileErrorUrl(safeReturnUrl, "invalid-birth-date", displayName, username));
            birthDate = parsed.Date;
        }

        var result = await userProfileService.UpdateCurrentAsync(
            context.User,
            displayName,
            username,
            birthDate,
            context.RequestAborted);

        if (result.Succeeded)
        {
            var separator = safeReturnUrl.Contains('?') ? '&' : '?';
            return Results.LocalRedirect(safeReturnUrl + separator + "profileUpdated=1");
        }

        return Results.LocalRedirect(
            BuildProfileErrorUrl(
                safeReturnUrl,
                ToProfileErrorCode(result.ErrorCode),
                displayName,
                username));
    }

    private static async Task<IResult> ChangePasswordAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IUserPasswordService userPasswordService,
        IAuthService authService)
    {
        await antiforgery.ValidateRequestAsync(context);

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var currentPassword = form["currentPassword"].ToString();
        var newPassword = form["newPassword"].ToString();
        var confirmPassword = form["confirmPassword"].ToString();
        var username = context.User.Identity?.Name ?? string.Empty;

        var result = await userPasswordService.ChangeCurrentUserPasswordAsync(
            context.User,
            currentPassword,
            newPassword,
            confirmPassword,
            context.RequestAborted);

        if (result.Succeeded)
        {
            await authService.LogoutAsync();

            var successUrl = "/?passwordChanged=1";
            if (!string.IsNullOrWhiteSpace(username))
            {
                successUrl += $"&username={Uri.EscapeDataString(username)}";
            }

            return Results.LocalRedirect(successUrl);
        }

        return Results.LocalRedirect(
            BuildChangePasswordErrorUrl(
                ToChangePasswordErrorCode(result.ErrorCode),
                result.ErrorDetails));
    }

    private static string ToChangePasswordErrorCode(PasswordChangeErrorCode errorCode) => errorCode switch
    {
        PasswordChangeErrorCode.RequiredFields => "required",
        PasswordChangeErrorCode.PasswordMismatch => "password-mismatch",
        PasswordChangeErrorCode.CurrentPasswordInvalid => "current-password",
        PasswordChangeErrorCode.WeakPassword => "password",
        PasswordChangeErrorCode.UserNotFound => "user-not-found",
        _ => "failed"
    };

    private static string ToLoginErrorCode(AuthErrorCode errorCode) => errorCode switch
    {
        AuthErrorCode.RequiredFields => "required",
        AuthErrorCode.LockedOut => "locked",
        _ => "invalid"
    };

    private static string ToSignupErrorCode(AuthErrorCode errorCode) => errorCode switch
    {
        AuthErrorCode.RequiredFields => "required",
        AuthErrorCode.DuplicateUsername => "duplicate-username",
        AuthErrorCode.DuplicateDisplayName => "duplicate-display-name",
        AuthErrorCode.InvalidUsername => "invalid-username",
        AuthErrorCode.DisplayNameTooLong => "display-name-too-long",
        AuthErrorCode.UsernameTooLong => "username-too-long",
        AuthErrorCode.WeakPassword => "password",
        AuthErrorCode.PasswordMismatch => "password-mismatch",
        AuthErrorCode.RoleCreationFailed => "role",
        _ => "failed"
    };

    private static string ToProfileErrorCode(UserProfileErrorCode errorCode) => errorCode switch
    {
        UserProfileErrorCode.InvalidBirthDate => "invalid-birth-date",
        UserProfileErrorCode.RequiredFields => "required",
        UserProfileErrorCode.DuplicateUsername => "duplicate-username",
        UserProfileErrorCode.DuplicateDisplayName => "duplicate-display-name",
        UserProfileErrorCode.InvalidUsername => "invalid-username",
        UserProfileErrorCode.DisplayNameTooLong => "display-name-too-long",
        UserProfileErrorCode.UsernameTooLong => "username-too-long",
        UserProfileErrorCode.UserNotFound => "user-not-found",
        _ => "failed"
    };

    private static string BuildLoginErrorUrl(string errorCode, string? username, string? returnUrl)
    {
        var query = new List<string>
        {
            $"error={Uri.EscapeDataString(errorCode)}"
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            query.Add($"username={Uri.EscapeDataString(username)}");
        }

        if (IsLocalUrl(returnUrl))
        {
            query.Add($"returnUrl={Uri.EscapeDataString(returnUrl!)}");
        }

        return "/?" + string.Join("&", query);
    }

    private static string BuildSignupErrorUrl(
        string errorCode,
        string? displayName,
        string? username,
        IEnumerable<string>? passwordErrors = null)
    {
        var query = new List<string>
        {
            $"error={Uri.EscapeDataString(errorCode)}"
        };

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            query.Add($"displayName={Uri.EscapeDataString(displayName)}");
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            query.Add($"username={Uri.EscapeDataString(username)}");
        }

        var passwordErrorList = passwordErrors?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (passwordErrorList is { Length: > 0 })
        {
            query.Add($"passwordErrors={Uri.EscapeDataString(string.Join(',', passwordErrorList))}");
        }

        return "/signup?" + string.Join("&", query);
    }

    private static string BuildChangePasswordErrorUrl(
        string errorCode,
        IEnumerable<string>? passwordErrors = null)
    {
        var query = new List<string>
        {
            $"error={Uri.EscapeDataString(errorCode)}"
        };

        var passwordErrorList = passwordErrors?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (passwordErrorList is { Length: > 0 })
        {
            query.Add($"passwordErrors={Uri.EscapeDataString(string.Join(',', passwordErrorList))}");
        }

        return "/change-password?" + string.Join("&", query);
    }

    private static string BuildProfileErrorUrl(
        string returnUrl,
        string errorCode,
        string displayName,
        string username)
    {
        var query = new List<string>
        {
            "profileEdit=1",
            $"profileError={Uri.EscapeDataString(errorCode)}",
            $"profileDisplayName={Uri.EscapeDataString(displayName)}",
            $"profileUsername={Uri.EscapeDataString(username)}"
        };

        var separator = returnUrl.Contains('?') ? '&' : '?';
        return returnUrl + separator + string.Join("&", query);
    }

    private static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (url[0] == '/')
        {
            return url.Length == 1 || (url[1] != '/' && url[1] != '\\');
        }

        return url.Length > 1 && url[0] == '~' && url[1] == '/';
    }
}
