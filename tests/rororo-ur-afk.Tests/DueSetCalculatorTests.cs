using Labs626.UrAfk.Core;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class DueSetCalculatorTests
{
    private sealed class FixedJitter : IJitterSource
    {
        public int Value;
        public int NextJitterSeconds() => Value;
    }

    private static DueCandidate C(string id, long idle, int pid = 100)
        => new(id, $"name-{id}", pid, idle);

    [Fact]
    public void Compute_ThresholdPlusJitterBoundary()
    {
        var jitter = new JitterBook(new FixedJitter { Value = 30 });
        var enabled = new HashSet<string> { "a", "b" };
        // threshold 900s + jitter 30s = due at >= 930
        var result = DueSetCalculator.Compute(
            new[] { C("a", 930), C("b", 929) }, enabled, 900, jitter);

        Assert.Single(result);
        Assert.Equal("a", result[0].AccountId);
    }

    [Fact]
    public void Compute_DisabledExcluded()
    {
        var jitter = new JitterBook(new FixedJitter { Value = 0 });
        var result = DueSetCalculator.Compute(
            new[] { C("a", 5000), C("b", 5000) },
            new HashSet<string> { "b" }, 900, jitter);

        Assert.Single(result);
        Assert.Equal("b", result[0].AccountId);
    }

    [Fact]
    public void Compute_OrdersMostIdleFirst()
    {
        var jitter = new JitterBook(new FixedJitter { Value = 0 });
        var enabled = new HashSet<string> { "a", "b", "c" };
        var result = DueSetCalculator.Compute(
            new[] { C("a", 1000), C("b", 3000), C("c", 2000) }, enabled, 900, jitter);

        Assert.Equal(new[] { "b", "c", "a" }, result.Select(r => r.AccountId).ToArray());
    }
}
