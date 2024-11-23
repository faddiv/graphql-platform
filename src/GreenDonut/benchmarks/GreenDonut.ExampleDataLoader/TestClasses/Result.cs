namespace GreenDonut.LoadTests.LoadTesting;

public record Result(int StatusCode, string? Message = null)
{
    public static Result Ok { get; } = new(200);
}
