using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class QuietHoursCalculatorTests
{
    [Theory]
    [InlineData("08:00", "09:00", "17:00", false)] // before window
    [InlineData("10:00", "10:00", "17:00", true)] // start inclusive
    [InlineData("10:00", "09:00", "10:00", false)] // end exclusive
    [InlineData("10:00", "05:00", "15:00", true)] // inside same-day window
    public void IsQuietNow_SameDayWindow(string now, string start, string end, bool expected)
    {
        var result = QuietHoursCalculator.IsQuietNow(TimeOnly.Parse(now), TimeOnly.Parse(start), TimeOnly.Parse(end));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("23:00", "21:00", "08:00", true)] // after start, before midnight
    [InlineData("02:00", "21:00", "08:00", true)] // after midnight, before end
    [InlineData("12:00", "21:00", "08:00", false)] // daytime, outside wrapped window
    [InlineData("21:00", "21:00", "08:00", true)] // start inclusive
    [InlineData("08:00", "21:00", "08:00", false)] // end exclusive
    public void IsQuietNow_WrapsPastMidnight(string now, string start, string end, bool expected)
    {
        var result = QuietHoursCalculator.IsQuietNow(TimeOnly.Parse(now), TimeOnly.Parse(start), TimeOnly.Parse(end));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsQuietNow_ZeroWidthWindow_NeverQuiet()
    {
        Assert.False(QuietHoursCalculator.IsQuietNow(TimeOnly.Parse("12:00"), TimeOnly.Parse("09:00"), TimeOnly.Parse("09:00")));
    }
}
