using System.Globalization;
using NTSW_CENOPS.Application.Events;

namespace NTSW_CENOPS.Components.Team;

/// <summary>
/// Groups intersecting time intervals, then assigns independent horizontal lanes.
/// Every connected overlap group shares its lane count. This allows consecutive
/// non-overlapping meetings to use full width without colliding with neighbors.
/// </summary>
public static class TimedEventLayout
{
    public sealed record Placement(CalendarOccurrence Event, string Style);

    private sealed record Slot(CalendarOccurrence Event, double Start, double End, int Lane = 0);

    public static IReadOnlyList<Placement> Arrange(IEnumerable<CalendarOccurrence> events, DateTime day)
    {
        var startOfDay = day.Date;
        var endOfDay = startOfDay.AddDays(1);
        var slots = events.Select(e =>
        {
            var visibleStart = e.Start > startOfDay ? e.Start : startOfDay;
            var finish = e.End ?? e.Start.AddMinutes(45);
            if (finish > endOfDay) finish = endOfDay;
            var start = Math.Clamp((visibleStart - startOfDay).TotalMinutes * .8, 0, 1152);
            var end = Math.Clamp(Math.Max(start + 25, (finish - startOfDay).TotalMinutes * .8), 0, 1152);
            return new Slot(e, start, end);
        }).Where(x => x.End > x.Start)
          .OrderBy(x => x.Start).ThenBy(x => x.End).ThenBy(x => x.Event.EventId).ToArray();

        var result = new List<Placement>(slots.Length);
        var group = new List<Slot>();
        double groupEnd = -1;
        void Flush()
        {
            if (group.Count == 0) return;
            var occupiedUntil = new List<double>();
            var assigned = new List<Slot>(group.Count);
            foreach (var slot in group)
            {
                var lane = occupiedUntil.FindIndex(end => end <= slot.Start);
                if (lane < 0) { lane = occupiedUntil.Count; occupiedUntil.Add(slot.End); }
                else occupiedUntil[lane] = slot.End;
                assigned.Add(slot with { Lane = lane });
            }
            var lanes = occupiedUntil.Count;
            foreach (var slot in assigned)
            {
                var right = (100.0 * slot.Lane / lanes).ToString("0.####", CultureInfo.InvariantCulture);
                var width = (100.0 / lanes).ToString("0.####", CultureInfo.InvariantCulture);
                var top = slot.Start.ToString("0.#", CultureInfo.InvariantCulture);
                var height = (slot.End - slot.Start).ToString("0.#", CultureInfo.InvariantCulture);
                result.Add(new Placement(slot.Event,
                    $"top:{top}px;height:{height}px;right:calc({right}% + 2px);width:calc({width}% - 4px);left:auto"));
            }
            group.Clear();
        }
        foreach (var slot in slots)
        {
            if (group.Count > 0 && slot.Start >= groupEnd) { Flush(); groupEnd = -1; }
            group.Add(slot);
            groupEnd = Math.Max(groupEnd, slot.End);
        }
        Flush();
        return result;
    }
}
