namespace GreenDonut.LoadTests.LoadTesting;

public class Results
{
    public required int Id { get; init; }
    public required long Duration { get; init; }
    public required bool Success { get; init; }
    public Exception? Exception { get; init; }
}
