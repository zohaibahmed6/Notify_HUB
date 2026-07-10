namespace NotifyHub.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
}
