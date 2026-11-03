namespace NTSW_CENOPS.Infrastructure.Jobs;

/// <summary>Midnight-anchored local clock schedule (not last-run + interval).</summary>
public static class JobSchedule
{
    public static DateTimeOffset? DueSlotUtc(DateTimeOffset nowUtc, int intervalMinutes, TimeZoneInfo zone)
    {
        if (intervalMinutes is < 1 or > 1440) throw new ArgumentOutOfRangeException(nameof(intervalMinutes));
        var local = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var wallClock = local.DateTime;
        var minuteOfDay = wallClock.Hour * 60 + wallClock.Minute;
        // Daily windows start at local midnight. The first within-day slot for
        // a 45-minute job is 00:45; a 00:00 slot closes the PREVIOUS day's
        // final window when the interval divides 24 hours.
        // A 1440-minute job is the special daily-at-midnight schedule.
        // Midnight closes the prior day for intervals that divide 24 hours;
        // without this, a six-hour job would miss 18:00–00:00 each day.
        if (minuteOfDay == 0 && 1440 % intervalMinutes != 0) return null;
        if (minuteOfDay != 0 && intervalMinutes != 1440 && minuteOfDay < intervalMinutes) return null;
        if (intervalMinutes == 1440 && minuteOfDay != 0) return null;
        var slotMinutes = intervalMinutes == 1440 ? 0 : (minuteOfDay / intervalMinutes) * intervalMinutes;
        var slotLocal = wallClock.Date.AddMinutes(slotMinutes);
        if (zone.IsInvalidTime(slotLocal)) slotLocal = slotLocal.AddMinutes(60);
        // When clocks repeat, choose the offset currently used by the incoming instant.
        var offset = zone.IsAmbiguousTime(slotLocal) ? local.Offset : zone.GetUtcOffset(slotLocal);
        return new DateTimeOffset(slotLocal, offset).ToUniversalTime();
    }
}
