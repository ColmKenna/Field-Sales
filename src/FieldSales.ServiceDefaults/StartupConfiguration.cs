using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

public static class StartupConfiguration
{
    /// <summary>Requires a configured value. allowBlank preserves hosts that previously checked only null.</summary>
    public static string Required(this IConfiguration configuration, string key, string? errorMessage = null,
        bool allowBlank = false)
    {
        string? value = configuration[key];
        if (value is null || !allowBlank && string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(errorMessage ??
                $"Configuration '{key}' is required and has no default. " +
                "Supply it through the AppHost, user secrets, or the environment.");
        return value;
    }

    public static string ResolveCertificatePath(string contentRoot, string configuredPath) =>
        Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(contentRoot, configuredPath);
}
