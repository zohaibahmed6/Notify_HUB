using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class PduSegmentCalculatorTests
{
    [Fact]
    public void CalculateSegmentCount_ShortGsm7Text_IsOneSegment()
    {
        Assert.Equal(1, PduSegmentCalculator.CalculateSegmentCount("Hi there, see you soon."));
    }

    [Fact]
    public void CalculateSegmentCount_ExactlySingleSegmentLimit_IsOneSegment()
    {
        var text = new string('a', 160);

        Assert.Equal(1, PduSegmentCalculator.CalculateSegmentCount(text));
    }

    [Fact]
    public void CalculateSegmentCount_OneOverSingleSegmentLimit_IsTwoSegments()
    {
        // 161 GSM-7 chars: exceeds the 160-char single-segment limit, so it splits using
        // the 153-char-per-segment multi-segment limit -> ceil(161/153) = 2.
        var text = new string('a', 161);

        Assert.Equal(2, PduSegmentCalculator.CalculateSegmentCount(text));
    }

    [Fact]
    public void CalculateSegmentCount_ThreeSegmentsWorthOfGsm7Text()
    {
        var text = new string('a', 153 * 2 + 1); // one char past 2 full segments

        Assert.Equal(3, PduSegmentCalculator.CalculateSegmentCount(text));
    }

    [Fact]
    public void CalculateSegmentCount_NonGsm7Character_UsesUcs2Limits()
    {
        // An emoji forces UCS-2; single-segment limit is 70, not 160.
        var text = new string('a', 70) + "🙂";

        Assert.Equal(2, PduSegmentCalculator.CalculateSegmentCount(text));
    }

    [Fact]
    public void CalculateSegmentCount_ShortUcs2Text_IsOneSegment()
    {
        Assert.Equal(1, PduSegmentCalculator.CalculateSegmentCount("See you soon 🙂"));
    }

    [Fact]
    public void CalculateSegmentCount_GsmExtendedCharacter_StaysGsm7()
    {
        // The Euro sign is in the GSM extension table, not the basic set — still GSM-7,
        // not UCS-2, so the 160-char limit applies.
        var text = new string('a', 159) + "€";

        Assert.Equal(1, PduSegmentCalculator.CalculateSegmentCount(text));
    }
}
