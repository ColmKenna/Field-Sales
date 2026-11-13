using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using FieldSales.Identity.Services.Users;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace FieldSales.Identity.Services.Diagnostics;

public sealed class DiagnosticsService(
    IIdentityDiagnosticsStore identityStore,
    ConfigurationDbContext configurationDbContext,
    PersistedGrantDbContext persistedGrantDbContext,
    IKeyMaterialService keyMaterialService,
    ReservedClaimTypePolicy reservedClaimTypes,
    ILogger<DiagnosticsService> logger) : IDiagnosticsService
{
    private readonly ConfigurationDbContext configurationDbContext = configurationDbContext;
    private readonly IIdentityDiagnosticsStore identityStore = identityStore;
    private readonly IKeyMaterialService keyMaterialService = keyMaterialService;
    private readonly ILogger<DiagnosticsService> logger = logger;
    private readonly PersistedGrantDbContext persistedGrantDbContext = persistedGrantDbContext;
    private readonly ReservedClaimTypePolicy reservedClaimTypes = reservedClaimTypes;

    public async Task<DiagnosticsModel> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var model = new DiagnosticsModel
        {
            StoreHealth =
            {
                await CheckStoreAsync("Identity Store", identityStore.CanConnectAsync, logger, cancellationToken),
                await CheckStoreAsync("Configuration Store", configurationDbContext.Database.CanConnectAsync, logger,
                    cancellationToken),
                await CheckStoreAsync("Operational Store", persistedGrantDbContext.Database.CanConnectAsync, logger,
                    cancellationToken)
            }
        };

        try
        {
            SigningCredentials? signingCredential =
                await keyMaterialService.GetSigningCredentialsAsync(null, cancellationToken);
            model.SigningKeyId = signingCredential?.Key.KeyId;
            model.SigningAlgorithm = signingCredential?.Algorithm;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load signing credentials from key material service.");
            model.SigningKeyId = "(error)";
            model.SigningAlgorithm = "(error)";
        }

        try
        {
            IReadOnlyCollection<SecurityKeyInfo> validationKeys =
                await keyMaterialService.GetValidationKeysAsync(cancellationToken);
            model.ActiveValidationKeys = validationKeys
                .Select(k => new SigningKeySummary
                {
                    KeyId = k.Key.KeyId ?? "(unknown)",
                    Algorithm = k.SigningAlgorithm,
                    IsX509Certificate = k.Key is X509SecurityKey
                })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load validation keys from key material service.");
            model.ActiveValidationKeys = [];
        }

        try
        {
            model.ReservedClaimHolders = await FindReservedClaimHoldersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query reserved claim holders.");
            model.ReservedClaimHolders = [];
        }

        return model;
    }

    /// <summary>
    ///     Reports user claims whose type the admin claim editor refuses to create — claims written
    ///     before that guard existed, or outside the admin UI.
    /// </summary>
    /// <remarks>
    ///     The reserved check is ordinal case-insensitive with a prefix rule, which no provider
    ///     translates to SQL reliably (SQLite compares strings case-sensitively by default, SQL Server
    ///     follows the column collation). So the distinct claim types are read first — a small set
    ///     whatever the row count — filtered in memory against the authoritative policy, and only the
    ///     matching types are then fetched with their holders.
    /// </remarks>
    private async Task<List<ReservedClaimHolder>> FindReservedClaimHoldersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> distinctClaimTypes =
            await identityStore.GetDistinctUserClaimTypesAsync(cancellationToken);

        var reservedClaimTypes = distinctClaimTypes
            .Where(t => this.reservedClaimTypes.IsReserved(t))
            .ToList();

        if (reservedClaimTypes.Count == 0)
            return new List<ReservedClaimHolder>();

        IReadOnlyList<ReservedClaimHolder> holders =
            await identityStore.GetClaimHoldersAsync(reservedClaimTypes, cancellationToken);
        return holders.ToList();
    }

    private static async Task<StoreHealthStatus> CheckStoreAsync(
        string name,
        Func<CancellationToken, Task<bool>> canConnect,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            bool connected = await canConnect(cancellationToken);
            return new StoreHealthStatus
            {
                Name = name,
                IsHealthy = connected,
                Detail = connected ? "Connected" : "Unable to connect to the store."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while checking connectivity for store '{StoreName}'.", name);
            return new StoreHealthStatus
            {
                Name = name,
                IsHealthy = false,
                Detail = "An error occurred while checking store health."
            };
        }
    }
}