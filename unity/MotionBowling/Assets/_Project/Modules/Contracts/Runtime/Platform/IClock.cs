namespace Bowling.Contracts
{
    public interface IClock { double Now { get; } }    // seconds, monotonic; Unity time in production, fake in tests
}
