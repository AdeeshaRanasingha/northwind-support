namespace SupportDesk.Infrastructure.Authentication;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; }

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience)
        && SigningKey.Length >= MinimumSigningKeyLength
        && ExpirationMinutes > 0;
}
