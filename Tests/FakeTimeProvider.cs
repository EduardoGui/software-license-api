namespace SoftwareLicense.Api.Tests;

public class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _fixedNow;

    public FakeTimeProvider(DateTimeOffset fixedNow)
    {
        _fixedNow = fixedNow;
    }

    public override DateTimeOffset GetUtcNow() => _fixedNow;
}
