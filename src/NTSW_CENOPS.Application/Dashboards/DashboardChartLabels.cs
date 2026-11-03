namespace NTSW_CENOPS.Application.Dashboards;

/// <summary>Human-readable report chart names shared by dashboard cards and the picker.</summary>
public static class DashboardChartLabels
{
    public static string Get(string? type) => type?.ToLowerInvariant() switch
    {
        "bar" => "ستونی", "line" => "خطی", "scatter" => "پراکندگی", "area" => "ناحیه‌ای",
        "step" => "پله‌ای", "stacked" => "ستونی انباشته", "smooth" => "منحنی نرم",
        "lollipop" => "میله نقطه‌ای", "stackedarea" => "ناحیه‌ای انباشته",
        "pie" => "دایره‌ای", "doughnut" => "حلقه‌ای", "treemap" => "نقشه درختی",
        "spider" => "عنکبوتی", "bubble" => "حبابی", "gauge" => "عقربه‌ای",
        _ => "نامشخص"
    };
}
