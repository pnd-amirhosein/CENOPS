using NTSW_CENOPS.Components.Common.Dates;

namespace NTSW_CENOPS.Components.Team;

/// <summary>Presentation-only Jalali calendar navigation; underlying dates remain Gregorian.</summary>
public static class CalendarDates
{
    public static DateTime MonthStart(DateTime gregorian) =>
        JalaliDate.Gregorian(JalaliDate.Parts(gregorian).Year, JalaliDate.Parts(gregorian).Month, 1);
    public static DateTime MonthStart(int year, int month) => JalaliDate.Gregorian(year, month, 1);
    public static DateTime NextMonth(DateTime date)
    {
        var (y, m, _) = JalaliDate.Parts(date);
        return m == 12 ? MonthStart(y + 1, 1) : MonthStart(y, m + 1);
    }
    public static DateTime PreviousMonth(DateTime date)
    {
        var (y, m, _) = JalaliDate.Parts(date);
        return m == 1 ? MonthStart(y - 1, 12) : MonthStart(y, m - 1);
    }
    public static DateTime StartOfWeek(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 1) % 7));
    public static IReadOnlyList<DateTime> MonthCells(int year, int month)
    {
        var start = StartOfWeek(MonthStart(year, month));
        var end = NextMonth(MonthStart(year, month));
        var length = (end - start).Days;
        var count = ((length + 6) / 7) * 7;
        return Enumerable.Range(0, count).Select(i => start.AddDays(i)).ToArray();
    }
    public static readonly string[] Weekdays = ["شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه"];
    public static string Heading(DateTime date) =>
        $"{JalaliDate.Month(JalaliDate.Parts(date).Month)} {JalaliDate.Parts(date).Year}";
}
