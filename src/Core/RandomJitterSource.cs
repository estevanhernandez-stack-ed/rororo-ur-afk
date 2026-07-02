namespace Labs626.UrAfk.Core;

public sealed class RandomJitterSource : IJitterSource
{
    private readonly int _maxSeconds;
    public RandomJitterSource(int maxSeconds) => _maxSeconds = Math.Max(0, maxSeconds);
    public int NextJitterSeconds() => _maxSeconds == 0 ? 0 : Random.Shared.Next(0, _maxSeconds + 1);
}
