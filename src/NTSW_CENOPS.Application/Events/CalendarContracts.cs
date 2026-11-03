using System.Security.Claims;

namespace NTSW_CENOPS.Application.Events;

public enum EventRepeatKind : byte { None, Daily, Weekly, Monthly, Yearly }
public enum EventCalendarBasis : byte { Jalali, Gregorian }

/// <summary>Wall-clock local dates. Persist Gregorian DateTime; Persian conversion belongs to the UI/recurrence calendar.</summary>
public sealed record CalendarEvent
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public EventRepeatKind Repeat { get; init; }
    public EventCalendarBasis CalendarBasis { get; init; } = EventCalendarBasis.Jalali;
    public int Interval { get; init; } = 1;
    /// <summary>Bit 0 = Sunday ... bit 6 = Saturday; 0 means event's start weekday.</summary>
    public int WeekdaysMask { get; init; }
    /// <summary>Optional comma-separated 1..31; empty means start day.</summary>
    public string MonthDays { get; init; } = "";
    /// <summary>Optional comma-separated 1..12; empty means start month.</summary>
    public string Months { get; init; } = "";
    public DateTime? RepeatUntil { get; init; }
    public int CreatedById { get; init; }
    public string CreatedByName { get; init; } = "";
}

public sealed record CalendarOccurrence(int EventId, string Title, string Description,
    DateTime Start, DateTime? End, string Author);

/// <summary>A selectable event or birthday; birthday IDs are negative user IDs.</summary>
public sealed record CalendarEventChoice(int Id, string Title, string? Author = null, bool IsBirthday = false);

public sealed record EventSaveResult(bool Succeeded, string Error, int Id = 0)
{
    public static EventSaveResult Ok(int id) => new(true, "", id);
    public static EventSaveResult Fail(string error) => new(false, error);
}

public interface ICalendarEventService
{
    Task<IReadOnlyList<CalendarOccurrence>> GetOccurrencesAsync(DateTime from, DateTime to, ClaimsPrincipal actor, CancellationToken ct = default);
    /// <summary>Visible recurring definitions for calendar/dashboard badges; no birthdays or deleted events.</summary>
    Task<IReadOnlyList<CalendarEvent>> GetRecurringAsync(IReadOnlyCollection<int> ids, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEventChoice>> GetChoicesAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEvent>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<CalendarEvent?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<EventSaveResult> SaveAsync(CalendarEvent item, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<EventSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
}
