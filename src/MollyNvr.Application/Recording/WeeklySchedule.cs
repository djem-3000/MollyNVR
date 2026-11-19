namespace MollyNvr.Application.Recording;

/// <summary>Weekly recording schedule: 7 days with 24 hourly slots each, starting on Monday.</summary>
public sealed class WeeklySchedule
{
    public const int DaysPerWeek = 7;
    public const int SlotsPerDay = 24;
    public const int SlotMinutes = 60;

    private const int SlotCount = DaysPerWeek * SlotsPerDay;
    private const int LegacySlotCount = DaysPerWeek * 48;

    private readonly bool[] _slots;

    private WeeklySchedule(bool[] slots) => _slots = slots;

        /// <summary>Empty schedule (nothing is recorded).</summary>
    public static WeeklySchedule Empty { get; } = new(new bool[SlotCount]);

        /// <summary>Around-the-clock schedule for all days.</summary>
    public static WeeklySchedule Always { get; } = new(Enumerable.Repeat(true, SlotCount).ToArray());

    public bool IsEmpty => !_slots.Contains(true);

    /// <summary>
        /// Parses a string of 168 '0'/'1' characters (7x24). Strings in the old
        /// 7x48 format are folded into hourly slots; an empty or broken string is an empty schedule.
    /// </summary>
    public static WeeklySchedule Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Empty;
        }

        if (value.Length == LegacySlotCount)
        {
            return FromLegacy(value);
        }

        if (value.Length != SlotCount)
        {
            return Empty;
        }

        var slots = new bool[SlotCount];

        for (var index = 0; index < SlotCount; index++)
        {
            slots[index] = value[index] == '1';
        }

        return new WeeklySchedule(slots);
    }

    private static WeeklySchedule FromLegacy(string value)
    {
        var slots = new bool[SlotCount];

        for (var day = 0; day < DaysPerWeek; day++)
        {
            for (var hour = 0; hour < SlotsPerDay; hour++)
            {
                var first = day * 48 + hour * 2;
                slots[day * SlotsPerDay + hour] = value[first] == '1' || value[first + 1] == '1';
            }
        }

        return new WeeklySchedule(slots);
    }

        /// <summary>A string of 336 '0'/'1' characters — for storage and passing to the UI.</summary>
    public string Serialize() => new(_slots.Select(static slot => slot ? '1' : '0').ToArray());

        /// <summary>Whether the slot is active at the specified moment (server local time).</summary>
    public bool IsActive(DateTimeOffset moment)
    {
        var local = moment.LocalDateTime;
        return _slots[Index(local.DayOfWeek, local.Hour, local.Minute)];
    }

        /// <summary>Whether the interval [<paramref name="from"/>, <paramref name="to"/>) overlaps selected slots.</summary>
    public bool Overlaps(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            return IsActive(from);
        }

        var slotStart = FloorToSlot(from);

                // Safety limit: no more than 8 days of iteration.
        for (var guard = 0; guard < DaysPerWeek * SlotsPerDay * 2 && slotStart < to; guard++)
        {
            if (IsActive(slotStart))
            {
                return true;
            }

            slotStart = slotStart.AddMinutes(SlotMinutes);
        }

        return false;
    }

        /// <summary>Start of the half-hour slot that contains the moment.</summary>
    public static DateTimeOffset FloorToSlot(DateTimeOffset moment)
    {
        var local = DateTime.SpecifyKind(moment.LocalDateTime, DateTimeKind.Unspecified);
        var floored = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute / SlotMinutes * SlotMinutes, 0,
            DateTimeKind.Local);

        return new DateTimeOffset(floored);
    }

        /// <summary>Day of the week in UI order: 0 — Monday, 6 — Sunday.</summary>
    public static int DayIndex(DayOfWeek day) => ((int)day + 6) % DaysPerWeek;

    private static int Index(DayOfWeek day, int hour, int minute) =>
        (DayIndex(day) * SlotsPerDay) + hour;
}
