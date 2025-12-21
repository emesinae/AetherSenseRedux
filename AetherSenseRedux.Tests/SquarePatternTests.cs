using AetherSenseRedux.Pattern;

namespace AetherSenseRedux.Tests;

[TestClass]
public sealed class SquarePatternTests
{
    [TestMethod]
    public void GetIntensityAtTime_EqualDurations_NoOffset()
    {
        var squarePatternConfig = new SquarePatternConfig()
        {
            Duration = 1000,
            Duration1 = 100,
            Duration2 = 100,
            Level1 = 0.25,
            Level2 = 0.5,
            Offset = 0,
        };
        var squarePattern = new SquarePattern(squarePatternConfig);
        // The "Zero" time.
        var originTime = squarePattern.Expires - TimeSpan.FromMilliseconds(squarePatternConfig.Duration);

        double GetIntensityAtTimeMs(long ms) => squarePattern.GetIntensityAtTime(originTime + TimeSpan.FromMilliseconds(ms));

        Assert.AreEqual(0.25, GetIntensityAtTimeMs(0));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(99));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(100));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(199));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(200));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(299));
    }

    [TestMethod]
    public void GetIntensityAtTime_EqualDurations_QuarterPeriodOffset()
    {
        var squarePatternConfig = new SquarePatternConfig()
        {
            Duration = 1000,
            Duration1 = 100,
            Duration2 = 100,
            Level1 = 0.25,
            Level2 = 0.5,
            Offset = 50,
        };
        var squarePattern = new SquarePattern(squarePatternConfig);
        // The "Zero" time.
        var originTime = squarePattern.Expires - TimeSpan.FromMilliseconds(squarePatternConfig.Duration);

        double GetIntensityAtTimeMs(long ms) => squarePattern.GetIntensityAtTime(originTime + TimeSpan.FromMilliseconds(ms));

        Assert.AreEqual(0.25, GetIntensityAtTimeMs(0));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(50));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(149));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(150));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(249));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(250));
    }

    [TestMethod]
    public void GetIntensityAtTime_EqualDurations_HalfPeriodOffset()
    {
        var squarePatternConfig = new SquarePatternConfig()
        {
            Duration = 1000,
            Duration1 = 100,
            Duration2 = 100,
            Level1 = 0.25,
            Level2 = 0.5,
            Offset = 100,
        };
        var squarePattern = new SquarePattern(squarePatternConfig);
        // The "Zero" time.
        var originTime = squarePattern.Expires - TimeSpan.FromMilliseconds(squarePatternConfig.Duration);

        double GetIntensityAtTimeMs(long ms) => squarePattern.GetIntensityAtTime(originTime + TimeSpan.FromMilliseconds(ms));

        Assert.AreEqual(0.5, GetIntensityAtTimeMs(0));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(99));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(100));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(199));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(200));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(299));
    }

    [TestMethod]
    public void GetIntensityAtTime_DifferentDurations_NoOffset()
    {
        var squarePatternConfig = new SquarePatternConfig()
        {
            Duration = 1000,
            Duration1 = 100,
            Duration2 = 200,
            Level1 = 0.25,
            Level2 = 0.5,
            Offset = 0,
        };
        var squarePattern = new SquarePattern(squarePatternConfig);
        // The "Zero" time.
        var originTime = squarePattern.Expires - TimeSpan.FromMilliseconds(squarePatternConfig.Duration);

        double GetIntensityAtTimeMs(long ms) => squarePattern.GetIntensityAtTime(originTime + TimeSpan.FromMilliseconds(ms));

        Assert.AreEqual(0.25, GetIntensityAtTimeMs(0));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(99));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(100));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(200));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(299));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(300));
        Assert.AreEqual(0.25, GetIntensityAtTimeMs(399));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(400));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(500));
        Assert.AreEqual(0.5, GetIntensityAtTimeMs(599));
    }

    [TestMethod]
    public void GetIntensityAtTime_ThrowsWhenOverDuration()
    {
        var squarePatternConfig = new SquarePatternConfig()
        {
            Duration = 1000,
            Duration1 = 100,
            Duration2 = 100,
            Level1 = 0.25,
            Level2 = 0.5,
            Offset = 0,
        };
        var squarePattern = new SquarePattern(squarePatternConfig);
        // The "Zero" time.
        var originTime = squarePattern.Expires - TimeSpan.FromMilliseconds(squarePatternConfig.Duration);

        double GetIntensityAtTimeMs(long ms) =>
            squarePattern.GetIntensityAtTime(originTime + TimeSpan.FromMilliseconds(ms));

        Assert.Throws<PatternExpiredException>(() => GetIntensityAtTimeMs(1001));
    }
}