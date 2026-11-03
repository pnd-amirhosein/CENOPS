using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace NTSW_CENOPS.Components.Pages;

public partial class Signup : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "displayName")]
    private string? DisplayNameFromQuery { get; set; }

    [SupplyParameterFromQuery(Name = "username")]
    private string? UsernameFromQuery { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    private string? ErrorCode { get; set; }

    [SupplyParameterFromQuery(Name = "passwordErrors")]
    private string? PasswordErrorsFromQuery { get; set; }

    private string DisplayName => DisplayNameFromQuery ?? string.Empty;
    private string Username => UsernameFromQuery ?? string.Empty;

    private string? Error => ErrorCode switch
    {
        "required" => "لطفا نام نمایشی، نام کاربری، رمز عبور و تکرار رمز عبور را وارد کنید",
        "display-name-too-long" => "نام نمایشی نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد",
        "username-too-long" => "نام کاربری نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد",
        "duplicate-display-name" => "این نام نمایشی قبلا استفاده شده است",
        "duplicate-username" => "این نام کاربری قبلا استفاده شده است",
        "invalid-username" => "نام کاربری معتبر نیست. از حروف انگلیسی، عدد و کاراکترهای - . _ @ + استفاده کنید",
        "password" => BuildPasswordError(),
        "password-mismatch" => "رمز عبور و تکرار آن یکسان نیستند",
        "role" => "نقش پیش‌فرض کاربر ایجاد نشد. لطفا دوباره تلاش کنید",
        "failed" => "ایجاد حساب انجام نشد. لطفا دوباره تلاش کنید",
        _ => null
    };

    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationStateTask is null)
        {
            return;
        }

        var authenticationState = await AuthenticationStateTask;

        if (authenticationState.User.Identity?.IsAuthenticated == true)
        {
            Navigation.NavigateTo("/dashboard", forceLoad: true);
        }
    }

    private string BuildPasswordError()
    {
        var codes = (PasswordErrorsFromQuery ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var rules = codes
            .Select(code => code switch
            {
                "PasswordTooShort" => "حداقل ۸ کاراکتر",
                "PasswordRequiresDigit" => "حداقل یک عدد",
                "PasswordRequiresLower" => "حداقل یک حرف کوچک انگلیسی",
                "PasswordRequiresUpper" => "حداقل یک حرف بزرگ انگلیسی",
                _ => null
            })
            .Where(x => x is not null)
            .Distinct()
            .ToArray();

        return rules.Length == 0
            ? "رمز عبور شرایط امنیتی لازم را ندارد"
            : $"رمز عبور معتبر نیست: {string.Join("، ", rules)}";
    }
}
