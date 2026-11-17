namespace MollyNvr.Application.Recording;

/// <summary>Недельное расписание записи: 7 дней по 24 часовых слота, с понедельника.</summary>
public sealed class WeeklySchedule
{
    public const int DaysPerWeek = 7;
    public const int SlotsPerDay = 24;
    public const int SlotMinutes = 60;

    private const int SlotCount = DaysPerWeek * SlotsPerDay;
    private const int LegacySlotCount = DaysPerWeek * 48;

    private readonly bool[] _slots;

    private WeeklySchedule(bool[] slots) => _slots = slots;

    /// <summary>Пустое расписание (ничего не записывается).</summary>
    public static WeeklySchedule Empty { get; } = new(new bool[SlotCount]);

    /// <summary>Круглосуточное расписание на все дни.</summary>
    public static WeeklySchedule Always { get; } = new(Enumerable.Repeat(true, SlotCount).ToArray());

    public bool IsEmpty => !_slots.Contains(true);

    /// <summary>
    /// Разбирает строку из 168 символов «0»/«1» (7×24). Строки старого формата
    /// 7×48 сворачиваются в часовые слоты; пустая или битая строка — пустое расписание.
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

    /// <summary>Строка из 336 символов «0»/«1» — для хранения и передачи в UI.</summary>
    public string Serialize() => new(_slots.Select(static slot => slot ? '1' : '0').ToArray());

    /// <summary>Активен ли слот в указанный момент (локальное время сервера).</summary>
    public bool IsActive(DateTimeOffset moment)
    {
        var local = moment.LocalDateTime;
        return _slots[Index(local.DayOfWeek, local.Hour, local.Minute)];
    }

    /// <summary>Пересекается ли интервал [<paramref name="from"/>, <paramref name="to"/>) с отмеченными слотами.</summary>
    public bool Overlaps(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            return IsActive(from);
        }

        var slotStart = FloorToSlot(from);

        // Ограничение на всякий случай: не больше 8 суток перебора.
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

    /// <summary>Начало получасового слота, в который попадает момент.</summary>
    public static DateTimeOffset FloorToSlot(DateTimeOffset moment)
    {
        var local = DateTime.SpecifyKind(moment.LocalDateTime, DateTimeKind.Unspecified);
        var floored = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute / SlotMinutes * SlotMinutes, 0,
            DateTimeKind.Local);

        return new DateTimeOffset(floored);
    }

    /// <summary>День недели в порядке UI: 0 — понедельник, 6 — воскресенье.</summary>
    public static int DayIndex(DayOfWeek day) => ((int)day + 6) % DaysPerWeek;

    private static int Index(DayOfWeek day, int hour, int minute) =>
        (DayIndex(day) * SlotsPerDay) + hour;
}
