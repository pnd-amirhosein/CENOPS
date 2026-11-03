using System.Globalization;

namespace NTSW_CENOPS.Components.Common.Dates;

/// <summary>
/// Presentation-only Persian calendar. DateTime, DateOnly and DateTimeOffset stay
/// Gregorian in database models, APIs, comparisons, filters and report calculations.
/// </summary>
public static class JalaliDate
{
    private static readonly PersianCalendar Calendar = new();
    private static readonly string[] MonthNames =
        ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];

    public static IReadOnlyList<string> Months => MonthNames;

    public static string Date(DateTime value) =>
        $"{Calendar.GetYear(value):0000}/{Calendar.GetMonth(value):00}/{Calendar.GetDayOfMonth(value):00}";
    public static string Date(DateOnly value) => Date(value.ToDateTime(TimeOnly.MinValue));
    public static string Date(DateTimeOffset value) => Date(value.DateTime);
    public static string DateTime(DateTime value, bool seconds = false) =>
        $"{Date(value)} {value.ToString(seconds ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture)}";
    public static string DateTime(DateTimeOffset value, bool seconds = false) => DateTime(value.DateTime, seconds);
    public static string? Date(DateTime? value) => value.HasValue ? Date(value.Value) : null;

    public static string Month(int month) => MonthNames[month - 1];
    public static (int Year, int Month, int Day) Parts(DateTime value) =>
        (Calendar.GetYear(value), Calendar.GetMonth(value), Calendar.GetDayOfMonth(value));
    public static int DaysInMonth(int year, int month) => Calendar.GetDaysInMonth(year, month);
    public static DateTime Gregorian(int year, int month, int day) =>
        Calendar.ToDateTime(year, month, day, 0, 0, 0, 0);

    // Report point labels are still stored with a Gregorian timestamp; only axis text changes.
    public static string AxisLabel(string label)
    {
        if (!System.DateTime.TryParseExact(label,
                ["yyyy/MM/dd HH:mm", "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return label;
        return label.Contains(':') ? DateTime(date) : Date(date);
    }
}
