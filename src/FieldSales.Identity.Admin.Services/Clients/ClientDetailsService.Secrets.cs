using FieldSales.Identity.Services.Persistence;
using System.Data;
using Duende.IdentityServer.EntityFramework.Entities;
using FieldSales.Identity.Services.AuditLogs;
using FieldSales.Identity.Services.Secrets;
using FieldSales.Identity.Services.Validation;
using Microsoft.EntityFrameworkCore;
using Client = Duende.IdentityServer.EntityFramework.Entities.Client;

namespace FieldSales.Identity.Services.Clients;

public partial class ClientDetailsService
{
    public async Task<ClientSecretsModel?> GetClientSecretsAsync(ClientId clientId,
        CancellationToken cancellationToken = default)
    {
        if (clientId.IsEmpty)
            return null;

        Client? client = await _configurationDbContext.Clients
            .AsNoTracking()
            .Include(c => c.ClientSecrets)
            .FirstOrDefaultAsync(c => c.ClientId == clientId.Value, cancellationToken);

        if (client is null)
            return null;

        return new ClientSecretsModel
        {
            ClientId = ClientId.Create(client.ClientId),
            ClientName = string.IsNullOrWhiteSpace(client.ClientName) ? client.ClientId : client.ClientName,
            RequireClientSecret = client.RequireClientSecret,
            Secrets = client.ClientSecrets
                .OrderBy(s => s.Id)
                .Select(s => new ClientSecretSummary
                {
                    Id = s.Id,
                    Description = s.Description,
                    Created = s.Created,
                    Expiration = s.Expiration
                })
                .ToList()
        };
    }

    public Task<ClientSecretGenerateResult> GenerateClientSecretAsync(ClientId clientId, string? description,
        DateTime? expiration = null, CancellationToken cancellationToken = default) =>
        ExecuteAuditedAsync(
            AuditAction.GenerateSecret,
            clientId.Value,
            clientId.Value,
            audit => GenerateClientSecretCoreAsync(clientId.Value, description, expiration, audit, cancellationToken),
            cancellationToken);

    private async Task<ClientSecretGenerateResult> GenerateClientSecretCoreAsync(string clientId, string? description,
        DateTime? expiration, AuditOperation audit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return await DenySecretClientNotFoundAsync(clientId, cancellationToken);

        string? trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null &&
            trimmedDescription.Length > ValidationConstants.MaxClientSecretDescriptionLength)
            return await DenySecretDescriptionTooLongAsync(clientId, cancellationToken);

        if (expiration.HasValue && expiration.Value.ToUniversalTime() <= _timeProvider.GetUtcNow().UtcDateTime)
            return await DenySecretExpirationInPastAsync(clientId, cancellationToken);

        string targetName = clientId;

        audit.TargetName = targetName;

        var outcome = await IdentityTransactions.RunAsync(_configurationDbContext, IsolationLevel.Serializable, async transaction =>
        {
            Client? client = await _configurationDbContext.Clients
                .Include(c => c.ClientSecrets)
                .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

            if (client is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ClientSecretGenerateResult.Failed("Client not found.");
            }

            targetName = client.ClientName ?? clientId;

            audit.TargetName = targetName;
            GeneratedClientSecret generated = ClientSecretFactory.Create(_timeProvider, trimmedDescription, expiration);
            client.ClientSecrets.Add(generated.Secret);

            await _configurationDbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClientSecretGenerateResult.Succeeded(generated.Plaintext);
        }, cancellationToken);

        if (!outcome.Success)
            return await DenySecretGenerationClientNotFoundAsync(clientId, targetName, cancellationToken);

        await _auditWriter.WriteAsync(new AdminAuditEvent(
            AuditCategory.Client, AuditAction.GenerateSecret, AuditOutcome.Succeeded, AuditReasonCode.Succeeded,
            clientId, targetName,
            Details: "Generated new client secret"), cancellationToken);

        return outcome;

    }

    private async Task<ClientSecretGenerateResult> DenySecretClientNotFoundAsync(string clientId, CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.GenerateSecret, AuditReasonCode.NotFound, clientId, clientId,
            "Client not found.", cancellationToken);
        return ClientSecretGenerateResult.Failed("Client not found.");
    }

    private async Task<ClientSecretGenerateResult> DenySecretDescriptionTooLongAsync(string clientId, CancellationToken cancellationToken)
    {
        string message =
            $"Secret description cannot exceed {ValidationConstants.MaxClientSecretDescriptionLength} characters.";
        await AuditDeniedAsync(AuditAction.GenerateSecret, AuditReasonCode.ValidationFailed, clientId, clientId,
            message, cancellationToken);
        return ClientSecretGenerateResult.ValidationFailure("Description", message);
    }

    private async Task<ClientSecretGenerateResult> DenySecretExpirationInPastAsync(string clientId, CancellationToken cancellationToken)
    {
        const string message = "Expiration date must be in the future.";
        await AuditDeniedAsync(AuditAction.GenerateSecret, AuditReasonCode.ValidationFailed, clientId, clientId,
            message, cancellationToken);
        return ClientSecretGenerateResult.ValidationFailure("Expiration", message);
    }

    private async Task<ClientSecretGenerateResult> DenySecretGenerationClientNotFoundAsync(
        string clientId, string targetName, CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.GenerateSecret, AuditReasonCode.NotFound, clientId, targetName,
            "Client not found.", cancellationToken);
        return ClientSecretGenerateResult.Failed("Client not found.");
    }

    public Task<ClientSecretRevokeResult> RevokeClientSecretAsync(ClientId clientId, int secretId,
        CancellationToken cancellationToken = default) =>
        ExecuteAuditedAsync(
            AuditAction.RevokeSecret,
            clientId.Value,
            clientId.Value,
            audit => RevokeClientSecretCoreAsync(clientId.Value, secretId, audit, cancellationToken),
            cancellationToken);

    private async Task<ClientSecretRevokeResult> RevokeClientSecretCoreAsync(string clientId, int secretId,
        AuditOperation audit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return await DenyRevokeSecretClientNotFoundAsync(clientId, cancellationToken);

        string targetName = clientId;

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        audit.TargetName = targetName;

        var outcome = await IdentityTransactions.RunAsync(_configurationDbContext, IsolationLevel.Serializable, async transaction =>
        {
            Client? client = await _configurationDbContext.Clients
                .Include(c => c.ClientSecrets)
                .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

            if (client is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ClientSecretRevokeResult.Failed(
                    "Client not found.", AuditReasonCode.NotFound, AdminMutationStatus.NotFound);
            }

            targetName = client.ClientName ?? clientId;

            audit.TargetName = targetName;
            ClientSecret? secret = client.ClientSecrets.FirstOrDefault(s => s.Id == secretId);
            if (secret is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ClientSecretRevokeResult.Failed(
                    "Secret not found.", AuditReasonCode.NotFound, AdminMutationStatus.NotFound);
            }

            bool hasUsableReplacement = client.ClientSecrets.Any(s =>
                s.Id != secretId && (!s.Expiration.HasValue || s.Expiration.Value > utcNow.UtcDateTime));
            if (client.RequireClientSecret && !hasUsableReplacement)
            {
                string message =
                    "This is the last usable secret on a confidential client and cannot be revoked. Generate a usable replacement secret first, or disable the client's secret requirement.";

                await transaction.RollbackAsync(cancellationToken);
                return ClientSecretRevokeResult.Failed(message, AuditReasonCode.LastUsableSecret);
            }

            client.ClientSecrets.Remove(secret);
            await _configurationDbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClientSecretRevokeResult.Succeeded();
        }, cancellationToken);

        if (!outcome.Success)
            return await DenyRevokeSecretFailedAsync(clientId, targetName, outcome, cancellationToken);

        await _auditWriter.WriteAsync(new AdminAuditEvent(
            AuditCategory.Client, AuditAction.RevokeSecret, AuditOutcome.Succeeded, AuditReasonCode.Succeeded,
            clientId, targetName,
            Details: "Revoked client secret"), cancellationToken);

        return outcome;

    }

    private async Task<ClientSecretRevokeResult> DenyRevokeSecretClientNotFoundAsync(string clientId, CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.RevokeSecret, AuditReasonCode.NotFound, clientId, clientId,
            "Client not found.", cancellationToken);
        return ClientSecretRevokeResult.Failed(
            "Client not found.", AuditReasonCode.NotFound, AdminMutationStatus.NotFound);
    }

    private async Task<ClientSecretRevokeResult> DenyRevokeSecretFailedAsync(
        string clientId, string targetName, ClientSecretRevokeResult outcome, CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.RevokeSecret, AuditReasonCode.From(outcome.ReasonCode), clientId, targetName,
            outcome.ErrorMessage!, cancellationToken);
        return outcome;
    }
}