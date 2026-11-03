using NTSW_CENOPS.Application.Events;

namespace NTSW_CENOPS.Infrastructure.Events;

public sealed class CalendarEventEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public EventRepeatKind Repeat { get; set; }
    public EventCalendarBasis CalendarBasis { get; set; }
    public int Interval { get; set; } = 1;
    public int WeekdaysMask { get; set; }
    public string MonthDays { get; set; } = "";
    public string Months { get; set; } = "";
    public DateTime? RepeatUntil { get; set; }
    public int CreatedById { get; set; }
    public bool IsDeleted { get; set; }
}
