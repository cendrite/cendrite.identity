namespace Identity.Application.Settings;

public sealed class SignupChallengeSettings
{
    public int Difficulty { get; init; } = 20;
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromMinutes(5);
}
