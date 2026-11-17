using MollyNvr.Application.Recording;

namespace MollyNvr.Application.Tests;

public class WeeklyScheduleTests
{
    [Fact]
    public void Parse_EmptyOrBroken_ReturnsEmptySchedule()
    {
        Assert.True(WeeklySchedule.Parse(null).IsEmpty);
        Assert.True(WeeklySchedule.Parse("").IsEmpty);
        Assert.True(WeeklySchedule.Parse("101").IsEmpty);
        Assert.True(WeeklySchedule.Parse("2" + new string('0', 167)).IsEmpty);
    }

    [Fact]
    public void Serialize_AndParse_RoundTrip()
    {
        var text = "1" + new string('0', 166) + "1";

        var schedule = WeeklySchedule.Parse(text);

        Assert.Equal(168, text.Length);
        Assert.False(schedule.IsEmpty);
        Assert.Equal(text, schedule.Serialize());
    }

    [Fact]
    public void Parse_LegacyHalfHourSchedule_CollapsesToHours()
    {
        // Старый формат 7×48: понедельник, 08:30 (второй получасовой слот часа 8).
        var legacy = new string('0', 17) + "1" + new string('0', 336 - 18);
        var schedule = WeeklySchedule.Parse(legacy);

        var monday = NextWeekday(DayOfWeek.Monday).Date;

        Assert.True(schedule.IsActive(new DateTimeOffset(monday.AddHours(8))));
        Assert.Equal(168, schedule.Serialize().Length);
    }

    [Fact]
    public void IsActive_UsesMondayFirstAndHourSlots()
    {
        var monday = NextWeekday(DayOfWeek.Monday).Date.AddHours(8).AddMinutes(15);
        const int bit = 8;
        var text = new string('0', bit) + "1" + new string('0', 167 - bit);
        var schedule = WeeklySchedule.Parse(text);

        Assert.True(schedule.IsActive(new DateTimeOffset(monday)));
        Assert.False(schedule.IsActive(new DateTimeOffset(monday.AddHours(1))));
    }

    [Fact]
    public void Overlaps_DetectsWindowInsideHour()
    {
        var monday = NextWeekday(DayOfWeek.Monday).Date.AddHours(10);
        const int bit = 10;
        var text = new string('0', bit) + "1" + new string('0', 167 - bit);
        var schedule = WeeklySchedule.Parse(text);

        Assert.True(schedule.Overlaps(new DateTimeOffset(monday.AddMinutes(10)), new DateTimeOffset(monday.AddMinutes(40))));
        Assert.False(schedule.Overlaps(new DateTimeOffset(monday.AddMinutes(-60)), new DateTimeOffset(monday.AddMinutes(-10))));
    }

    [Fact]
    public void Always_ContainsEveryMoment()
    {
        Assert.True(WeeklySchedule.Always.IsActive(DateTimeOffset.Now));
        Assert.False(WeeklySchedule.Always.IsEmpty);
    }

    private static DateTime NextWeekday(DayOfWeek day)
    {
        var date = DateTime.Today;

        while (date.DayOfWeek != day)
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
