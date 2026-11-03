using Microsoft.AspNetCore.Components;

namespace NTSW_CENOPS.Components.Pages;

public partial class ChangePassword : ComponentBase
{
    [SupplyParameterFromQuery(Name = "error")]
    private string? ErrorCode { get; set; }

    [SupplyParameterFromQuery(Name = "passwordErrors")]
    private string? PasswordErrorsFromQuery { get; set; }

    private string? Error => ErrorCode switch
    {
        "required" => "رمز عبور فعلی، رمز جدید و تکرار رمز جدید را وارد کنید",
        "password-mismatch" => "رمز عبور جدید و تکرار آن یکسان نیستند",
        "current-password" => "رمز عبور فعلی نادرست است",
        "password" => BuildPasswordError(),
        "user-not-found" => "اطلاعات حساب کاربری پیدا نشد. لطفا دوباره وارد شوید",
        "failed" => "تغییر رمز عبور انجام نشد. لطفا دوباره تلاش کنید",
        _ => null
    };

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
            ? "رمز عبور جدید شرایط امنیتی لازم را ندارد"
            : $"رمز عبور جدید معتبر نیست: {string.Join("، ", rules)}";
    }
}
