using FluentAssertions;
using Identity.Application.Services;

namespace Identity.Test.Unit.ServiceTest;

public class ProofOfWorkServiceTest
{
    private static readonly byte[] Challenge = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private readonly ProofOfWorkService _sut = new();

    [Theory]
    [InlineData(537ul, 8)]
    [InlineData(1755ul, 12)]
    [InlineData(13402ul, 16)]
    [InlineData(1111622ul, 20)]
    public void IsSolved_Given_Known_Solution_Should_Return_True(ulong nonce, int difficulty)
    {
        _sut.IsSolved(Challenge, nonce, difficulty).Should().BeTrue();
    }

    [Theory]
    [InlineData(536ul, 8)]
    [InlineData(1754ul, 12)]
    [InlineData(13401ul, 16)]
    [InlineData(1111621ul, 20)]
    public void IsSolved_Given_Wrong_Nonce_Should_Return_False(ulong nonce, int difficulty)
    {
        _sut.IsSolved(Challenge, nonce, difficulty).Should().BeFalse();
    }

    [Fact]
    public void IsSolved_Given_Solution_For_Lower_Difficulty_Should_Return_False()
    {
        _sut.IsSolved(Challenge, 537ul, 20).Should().BeFalse();
    }

    [Fact]
    public void IsSolved_Given_Zero_Difficulty_Should_Return_True()
    {
        _sut.IsSolved(Challenge, 0ul, 0).Should().BeTrue();
    }
}
