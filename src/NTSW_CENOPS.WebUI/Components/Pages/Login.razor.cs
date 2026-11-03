using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace NTSW_CENOPS.Components.Pages;

public partial class Login : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "username")]
    private string? UsernameFromQuery { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    private string? ErrorCode { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrlFromQuery { get; set; }

    [SupplyParameterFromQuery(Name = "registered")]
    private string? Registered { get; set; }

    [SupplyParameterFromQuery(Name = "passwordChanged")]
    private string? PasswordChanged { get; set; }

    private string Username => UsernameFromQuery ?? string.Empty;

    private string ReturnUrl => IsLocalUrl(ReturnUrlFromQuery)
        ? ReturnUrlFromQuery!
        : "/dashboard";

    private string? Error => ErrorCode switch
    {
        "required" => "لطفا نام کاربری و یا رمز عبور را وارد کنید",
        "invalid" => "نام کاربری یا رمز عبور نادرست است",
        "locked" => "حساب کاربری موقتا قفل شده است. لطفا کمی بعد دوباره تلاش کنید",
        _ => null
    };

    private string? Success => PasswordChanged == "1"
        ? "رمز عبور با موفقیت تغییر کرد. لطفا دوباره وارد شوید"
        : Registered == "1"
            ? "حساب کاربری با موفقیت ایجاد شد. حالا می‌توانید وارد شوید"
            : null;

    private string? StatusMessage => Success ?? Error;

    private string StatusCssClass => Success is not null
        ? "text-green-500"
        : "text-red-500";

    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationStateTask is null)
        {
            return;
        }

        var authenticationState = await AuthenticationStateTask;

        if (authenticationState.User.Identity?.IsAuthenticated == true)
        {
            Navigation.NavigateTo(ReturnUrl, forceLoad: true);
        }
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
