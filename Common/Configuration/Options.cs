namespace Vyracare.Api.Appointments.Common.Configuration;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "vyracare_db";
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public string AllowedOrigins { get; set; } = "*";
}

public sealed class AppointmentOptions
{
    public const string SectionName = "Appointments";
    public string TimeZone { get; set; } = "America/Sao_Paulo";
    public int WeeklyAvailableMinutes { get; set; } = 2400;
    public int FollowUpWindowDays { get; set; } = 3;
}
