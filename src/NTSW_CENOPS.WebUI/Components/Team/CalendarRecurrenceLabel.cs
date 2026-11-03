using NTSW_CENOPS.Application.Events;
using NTSW_CENOPS.Components.Common.Dates;

namespace NTSW_CENOPS.Components.Team;

/// <summary>Persian presentation only; never modifies Gregorian recurrence data.</summary>
public static class CalendarRecurrenceLabel
{
    /// <summary>Short, legible event-list label. Detailed recurrence remains in the details modal.</summary>
    public static string Brief(CalendarEvent e)
    {
        if (e.Repeat == EventRepeatKind.None) return "";
        var unit = e.Repeat switch
        {
            EventRepeatKind.Daily => "روز", EventRepeatKind.Weekly => "هفته",
            EventRepeatKind.Monthly => "ماه", _ => "سال"
        };
        var name = e.Interval <= 1 ? e.Repeat switch
        {
            EventRepeatKind.Daily => "روزانه", EventRepeatKind.Weekly => "هفتگی",
            EventRepeatKind.Monthly => "ماهانه", _ => "سالانه"
        } : $"هر {e.Interval} {unit}";
        return $"تکرارشونده · {name}";
    }

    public static string Get(CalendarEvent e)
    {
        string name = e.Repeat switch
        {
            EventRepeatKind.None => "بدون تکرار",
            EventRepeatKind.Daily => "روزانه",
            EventRepeatKind.Weekly => "هفتگی",
            EventRepeatKind.Monthly => "ماهانه",
            _ => "سالانه"
        };
        if (e.Repeat == EventRepeatKind.None) return name;
        string[] weekNames = ["یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه"];
        var interval = e.Interval > 1 ? $" (هر {e.Interval} دوره)" : "";
        var weekdays = e.Repeat == EventRepeatKind.Weekly && e.WeekdaysMask != 0
            ? " · " + string.Join("، ", Enumerable.Range(0, 7)
                .Where(i => (e.WeekdaysMask & (1 << i)) != 0).Select(i => weekNames[i])) : "";
        var days = !string.IsNullOrWhiteSpace(e.MonthDays) ? $" · روزهای {e.MonthDays}" : "";
        var months = !string.IsNullOrWhiteSpace(e.Months) ? $" · ماه‌های {e.Months}" : "";
        var basis = e.Repeat is EventRepeatKind.Monthly or EventRepeatKind.Yearly
            ? $" · تقویم {(e.CalendarBasis == EventCalendarBasis.Jalali ? "شمسی" : "میلادی")}" : "";
        var until = e.RepeatUntil.HasValue ? $" · تا {JalaliDate.Date(e.RepeatUntil.Value)}" : "";
        return $"{name}{interval}{weekdays}{days}{months}{basis}{until}";
    }
}
