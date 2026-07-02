using Labs626.UrAfk.Core;
using Xunit;

namespace Labs626.UrAfk.Tests;

public class JitterBookTests
{
    private sealed class SeqJitter : IJitterSource
    {
        private readonly Queue<int> _values;
        public SeqJitter(params int[] values) => _values = new Queue<int>(values);
        public int NextJitterSeconds() => _values.Dequeue();
    }

    [Fact]
    public void GetOrAssign_StableUntilReroll()
    {
        var book = new JitterBook(new SeqJitter(30, 70));
        Assert.Equal(30, book.GetOrAssign("a"));
        Assert.Equal(30, book.GetOrAssign("a"));   // stable
        book.Reroll("a");
        Assert.Equal(70, book.GetOrAssign("a"));   // new draw
    }

    [Fact]
    public void Forget_DropsAssignment()
    {
        var book = new JitterBook(new SeqJitter(10, 20));
        _ = book.GetOrAssign("a");
        book.Forget("a");
        Assert.Equal(20, book.GetOrAssign("a"));   // fresh draw after forget
    }
}
