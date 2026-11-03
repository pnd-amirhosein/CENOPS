using System.Security.Claims;
using System.Globalization;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Events;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Events;

public sealed class CalendarEventService(IDbContextFactory<AppDbContext> factory, IAccessService access) : ICalendarEventService
{
    private Task<bool> Allowed(ClaimsPrincipal actor, CancellationToken ct) => access.HasAccessAsync(actor, AccessKeys.Events, ct);
    private static int UserId(ClaimsPrincipal actor) => int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IReadOnlyList<CalendarOccurrence>> GetOccurrencesAsync(DateTime from, DateTime to, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return [];
        if (to <= from || (to - from).TotalDays > 370) throw new ArgumentOutOfRangeException(nameof(to), "تقویم باید در بازه حداکثر یک‌سال بارگذاری شود.");
        await using var db = await factory.CreateDbContextAsync(ct);
        // An occurrence can start before the window if its event spans multiple days (capped at 31 days).
        var earliest = from.AddDays(-31);
        var entities = await (from e in db.CalendarEvents.AsNoTracking()
                              join user in db.Users.IgnoreQueryFilters().AsNoTracking() on e.CreatedById equals user.Id
                              where !e.IsDeleted && e.StartsAt < to && (e.Repeat != EventRepeatKind.None || e.StartsAt >= earliest)
                              select new { Event = e, Author = user.DisplayName }).ToListAsync(ct);
        var occurrences = entities.SelectMany(x => EventRecurrence.Expand(ToModel(x.Event, x.Author), from, to)).ToList();
        // Birthdates are persisted in Gregorian; their annual anniversary is on
        // the same *Jalali* month/day users chose in the Persian date picker.
        var people = await db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.BirthDate.HasValue)
            .Select(u => new { u.Id, u.DisplayName, u.BirthDate }).ToListAsync(ct);
        var persian = new PersianCalendar();
        var firstYear = persian.GetYear(from.Date);
        var lastYear = persian.GetYear(to.Date);
        foreach (var user in people)
        {
            var born = user.BirthDate!.Value;
            var month = persian.GetMonth(born);
            var day = persian.GetDayOfMonth(born);
            for (var year = firstYear; year <= lastYear; year++)
            {
                if (day > persian.GetDaysInMonth(year, month)) continue; // Esfand 30 in non-leap years.
                var date = persian.ToDateTime(year, month, day, 0, 0, 0, 0);
                if (date >= from && date < to)
                    occurrences.Add(new CalendarOccurrence(-user.Id, $"🎂 زادروز {user.DisplayName} 🎉",
                        "زادروز همکار عزیزمان؛ با آرزوی شادی و تندرستی!", date, null, user.DisplayName));
            }
        }
        return occurrences.OrderBy(x => x.Start).ThenBy(x => x.Title).ToArray();
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetRecurringAsync(IReadOnlyCollection<int> ids, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (ids.Count == 0 || !await Allowed(actor, ct)) return [];
        var unique = ids.Where(x => x > 0).Distinct().Take(100).ToArray();
        if (unique.Length == 0) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        var entries = await (from e in db.CalendarEvents.AsNoTracking()
                             join u in db.Users.AsNoTracking() on e.CreatedById equals u.Id
                             where unique.Contains(e.Id) && !e.IsDeleted && e.Repeat != EventRepeatKind.None
                             select new { Event = e, Author = u.DisplayName }).ToListAsync(ct);
        return entries.Select(x => ToModel(x.Event, x.Author)).ToArray();
    }

    public async Task<IReadOnlyList<CalendarEventChoice>> GetChoicesAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        var events = await (from e in db.CalendarEvents.AsNoTracking()
                            join u in db.Users.AsNoTracking() on e.CreatedById equals u.Id
                            where !e.IsDeleted
                            orderby e.Title
                            select new CalendarEventChoice(e.Id, e.Title, u.DisplayName, false)).ToListAsync(ct);
        var birthdays = await db.Users.AsNoTracking().Where(u => !u.IsDeleted && u.BirthDate.HasValue)
            .OrderBy(u => u.DisplayName)
            .Select(u => new CalendarEventChoice(-u.Id, "🎂 زادروز " + u.DisplayName, u.DisplayName, true))
            .ToListAsync(ct);
        return events.Concat(birthdays).ToList();
    }

    public async Task<IReadOnlyList<CalendarEvent>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await (from e in db.CalendarEvents.AsNoTracking()
                           join u in db.Users.IgnoreQueryFilters().AsNoTracking() on e.CreatedById equals u.Id
                           where !e.IsDeleted
                           orderby e.StartsAt descending
                           select new { Event = e, Author = u.DisplayName }).ToListAsync(ct);
        return items.Select(x => ToModel(x.Event, x.Author)).ToArray();
    }

    public async Task<CalendarEvent?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var found = await (from e in db.CalendarEvents.AsNoTracking()
                           join user in db.Users.IgnoreQueryFilters().AsNoTracking() on e.CreatedById equals user.Id
                           where e.Id == id && !e.IsDeleted select new { Event = e, Author = user.DisplayName }).FirstOrDefaultAsync(ct);
        return found is null ? null : ToModel(found.Event, found.Author);
    }

    public async Task<EventSaveResult> SaveAsync(CalendarEvent item, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct) || UserId(actor) == 0) return EventSaveResult.Fail("دسترسی به ثبت رویداد ندارید.");
        var title = item.Title.Trim();
        if (title.Length is < 1 or > 180 || item.Description.Length > 3000)
            return EventSaveResult.Fail("عنوان (۱ تا ۱۸۰ نویسه) و توضیح (حداکثر ۳۰۰۰ نویسه) را بررسی کنید.");
        if (!Enum.IsDefined(item.Repeat) || !Enum.IsDefined(item.CalendarBasis) || item.Interval is < 1 or > 365 || item.WeekdaysMask is < 0 or > 127)
            return EventSaveResult.Fail("قانون تکرار معتبر نیست.");
        if (item.EndsAt is { } end && (end <= item.StartsAt || end - item.StartsAt > TimeSpan.FromDays(31)))
            return EventSaveResult.Fail("پایان باید بعد از شروع و فاصله رویداد حداکثر ۳۱ روز باشد.");
        if (item.RepeatUntil.HasValue && item.RepeatUntil.Value.Date < item.StartsAt.Date)
            return EventSaveResult.Fail("پایان تکرار نباید قبل از تاریخ شروع باشد.");
        if (item.MonthDays.Length > 100 || item.Months.Length > 50 || !ValidCsv(item.MonthDays, 31) || !ValidCsv(item.Months, 12))
            return EventSaveResult.Fail("روزها یا ماه‌های تکرار معتبر نیستند.");
        await using var db = await factory.CreateDbContextAsync(ct);
        CalendarEventEntity entity;
        if (item.Id == 0)
        {
            entity = new CalendarEventEntity { CreatedById = UserId(actor) };
            db.CalendarEvents.Add(entity);
        }
        else
        {
            entity = await db.CalendarEvents.FirstOrDefaultAsync(x => x.Id == item.Id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("رویداد پیدا نشد.");
            if (entity.CreatedById != UserId(actor) && !await IsManagerAsync(db, UserId(actor), ct))
                return EventSaveResult.Fail("فقط سازنده یا مدیر می‌تواند رویداد را ویرایش کند.");
        }
        entity.Title = title;
        entity.Description = item.Description.Trim();
        entity.StartsAt = item.StartsAt;
        entity.EndsAt = item.EndsAt;
        entity.Repeat = item.Repeat;
        entity.CalendarBasis = item.CalendarBasis;
        entity.Interval = item.Interval;
        entity.WeekdaysMask = item.WeekdaysMask;
        entity.MonthDays = EventRecurrence.NormalizeNumbers(item.MonthDays, 31);
        entity.Months = EventRecurrence.NormalizeNumbers(item.Months, 12);
        entity.RepeatUntil = item.Repeat == EventRepeatKind.None ? null : item.RepeatUntil?.Date;
        await db.SaveChangesAsync(ct);
        return EventSaveResult.Ok(entity.Id);
    }

    public async Task<EventSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return EventSaveResult.Fail("دسترسی ندارید.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.CalendarEvents.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null) return EventSaveResult.Fail("رویداد پیدا نشد.");
        if (entity.CreatedById != UserId(actor) && !await IsManagerAsync(db, UserId(actor), ct))
            return EventSaveResult.Fail("فقط سازنده یا مدیر می‌تواند رویداد را حذف کند.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return EventSaveResult.Ok(id);
    }

    private static bool ValidCsv(string? csv, int max) => EventRecurrence.AsciiDigits(csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .All(x => int.TryParse(x, out var value) && value >= 1 && value <= max);

    private static Task<bool> IsManagerAsync(AppDbContext db, int userId, CancellationToken ct) =>
        (from m in db.UserRoles.AsNoTracking() join r in db.Roles.AsNoTracking() on m.RoleId equals r.Id
         where m.UserId == userId && (r.NormalizedName == "ADMIN" || r.NormalizedName == "OWNER") select m.UserId).AnyAsync(ct);

    private static CalendarEvent ToModel(CalendarEventEntity e, string author) => new()
    {
        Id = e.Id, Title = e.Title, Description = e.Description, StartsAt = e.StartsAt, EndsAt = e.EndsAt,
        Repeat = e.Repeat, CalendarBasis = e.CalendarBasis, Interval = e.Interval, WeekdaysMask = e.WeekdaysMask,
        MonthDays = e.MonthDays, Months = e.Months, RepeatUntil = e.RepeatUntil,
        CreatedById = e.CreatedById, CreatedByName = author
    };
}
