namespace GreenDonut.ExampleDataLoader.TestClasses;

public record Result(int StatusCode, string? Message = null)
{
    public static Result Ok { get; } = new(200);
}
