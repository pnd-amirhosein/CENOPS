using System.Globalization;
using NTSW_CENOPS.Application.Events;

namespace NTSW_CENOPS.Infrastructure.Events;

/// <summary>Bounded on-demand occurrence expansion; no materialized repeating rows or hidden date conversion.</summary>
public static class EventRecurrence
{
    private static readonly PersianCalendar Persian = new();

    public static IReadOnlyList<CalendarOccurrence> Expand(CalendarEvent item, DateTime from, DateTime to)
    {
        if (to <= from) return [];
        var duration = item.EndsAt.HasValue ? item.EndsAt.Value - item.StartsAt : TimeSpan.Zero;
        if (duration < TimeSpan.Zero) return [];
        var first = item.Repeat == EventRepeatKind.None ? item.StartsAt.Date : from.Date.AddDays(-Math.Ceiling(duration.TotalDays));
        if (first < item.StartsAt.Date) first = item.StartsAt.Date;
        var until = item.RepeatUntil?.Date ?? DateTime.MaxValue.Date;
        var stop = to.Date < until ? to.Date : until;
        var results = new List<CalendarOccurrence>();
        for (var date = first; date <= stop; date = date.AddDays(1))
        {
            if (item.Repeat == EventRepeatKind.None && date != item.StartsAt.Date) break;
            if (!OccursOn(item, date)) continue;
            var start = date + item.StartsAt.TimeOfDay;
            var end = item.EndsAt is null ? (DateTime?)null : start + duration;
            if (start >= to || (end.HasValue ? end.Value <= from : start < from)) continue;
            results.Add(new CalendarOccurrence(item.Id, item.Title, item.Description, start, end, item.CreatedByName));
            if (item.Repeat == EventRepeatKind.None) break;
        }
        return results;
    }

    public static bool OccursOn(CalendarEvent item, DateTime date)
    {
        date = date.Date;
        var start = item.StartsAt.Date;
        if (date < start || (item.RepeatUntil.HasValue && date > item.RepeatUntil.Value.Date)) return false;
        var interval = Math.Max(1, item.Interval);
        switch (item.Repeat)
        {
            case EventRepeatKind.None: return date == start;
            case EventRepeatKind.Daily: return (date - start).Days % interval == 0;
            case EventRepeatKind.Weekly:
                var startWeek = start.AddDays(-(((int)start.DayOfWeek + 1) % 7)); // Saturday-based weeks
                var targetWeek = date.AddDays(-(((int)date.DayOfWeek + 1) % 7));
                var weekDiff = (targetWeek - startWeek).Days / 7;
                var mask = item.WeekdaysMask == 0 ? 1 << (int)start.DayOfWeek : item.WeekdaysMask;
                return weekDiff % interval == 0 && (mask & (1 << (int)date.DayOfWeek)) != 0;
            case EventRepeatKind.Monthly:
            case EventRepeatKind.Yearly:
                var s = CalendarParts(start, item.CalendarBasis);
                var d = CalendarParts(date, item.CalendarBasis);
                var diff = (d.Year - s.Year) * 12 + d.Month - s.Month;
                if (diff < 0) return false;
                if (item.Repeat == EventRepeatKind.Monthly && diff % interval != 0) return false;
                if (item.Repeat == EventRepeatKind.Yearly && (d.Year - s.Year) % interval != 0) return false;
                var months = ParseNumbers(item.Months, 12);
                var days = ParseNumbers(item.MonthDays, 31);
                if (months.Count == 0 && item.Repeat == EventRepeatKind.Yearly) months.Add(s.Month);
                if (days.Count == 0) days.Add(s.Day);
                return (months.Count == 0 || months.Contains(d.Month)) && days.Contains(d.Day);
            default: return false;
        }
    }

    private static (int Year, int Month, int Day) CalendarParts(DateTime date, EventCalendarBasis basis) =>
        basis == EventCalendarBasis.Gregorian
            ? (date.Year, date.Month, date.Day)
            : (Persian.GetYear(date), Persian.GetMonth(date), Persian.GetDayOfMonth(date));

    public static HashSet<int> ParseNumbers(string? csv, int max)
    {
        var result = new HashSet<int>();
        foreach (var part in AsciiDigits(csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= max)
                result.Add(n);
        return result;
    }

    /// <summary>Accept Persian/Arabic-Indic numeric input from RTL editor fields.</summary>
    public static string AsciiDigits(string value) => new(value.Select(c =>
        c is >= '\u06F0' and <= '\u06F9' ? (char)('0' + c - '\u06F0') :
        c is >= '\u0660' and <= '\u0669' ? (char)('0' + c - '\u0660') : c).ToArray());

    public static string NormalizeNumbers(string? csv, int max) =>
        string.Join(',', ParseNumbers(csv, max).OrderBy(x => x));
}
