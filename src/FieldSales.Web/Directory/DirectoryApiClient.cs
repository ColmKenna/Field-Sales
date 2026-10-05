using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using Microsoft.AspNetCore.Authentication;

namespace FieldSales.Web.Directory;

public sealed record DirectoryResult<T>(HttpStatusCode Status, T? Value = default, string? Error = null,
    string? Field = null, bool RequiresDuplicateConfirmation = false, MainContactConfirmation? Confirmation = null)
{
    public bool Success => (int)Status is >= 200 and < 300 && Value is not null;
}

public sealed class DirectoryApiClient(HttpClient client, IHttpContextAccessor contexts)
{
    private sealed record ApiError(string Error, string? Field = null, bool RequiresDuplicateConfirmation = false,
        MainContactConfirmation? Confirmation = null);
    private const string Root = "/directory/geography";
    public Task<DirectoryResult<ContactDetails>> ContactAsync(Guid id, CancellationToken ct) =>
        SendAsync<ContactDetails>(HttpMethod.Get, $"/directory/contacts/{id}", null, ct);
    public Task<DirectoryResult<ContactChoice[]>> ContactChoicesAsync(CancellationToken ct) =>
        SendAsync<ContactChoice[]>(HttpMethod.Get, "/directory/contacts/choices", null, ct);
    public Task<DirectoryResult<DirectoryTypeChoice[]>> ContactTypeChoicesAsync(CancellationToken ct) =>
        SendAsync<DirectoryTypeChoice[]>(HttpMethod.Get, "/directory/contacts/type-choices", null, ct);
    public Task<DirectoryResult<ContactLocationChoice[]>> ContactLocationChoicesAsync(CancellationToken ct) =>
        SendAsync<ContactLocationChoice[]>(HttpMethod.Get, "/directory/contacts/location-choices", null, ct);
    public Task<DirectoryResult<LocationContactsPage>> LocationContactsAsync(Guid id, bool showInactive, CancellationToken ct) =>
        SendAsync<LocationContactsPage>(HttpMethod.Get, $"/directory/locations/{id}/contacts?showInactive={showInactive}", null, ct);
    public Task<DirectoryResult<ContactDetails>> CreateContactAsync(CreateContactRequest request, CancellationToken ct) =>
        SendAsync<ContactDetails>(HttpMethod.Post, "/directory/contacts", JsonContent.Create(request), ct);
    public Task<DirectoryResult<ContactDetails>> EditContactAsync(Guid id, EditContactRequest request, CancellationToken ct) =>
        SendAsync<ContactDetails>(HttpMethod.Put, $"/directory/contacts/{id}", JsonContent.Create(request), ct);
    public Task<DirectoryResult<ContactMutationResult>> RemoveContactAsync(Guid locationId, Guid contactId, RemoveLocationContactRequest request, CancellationToken ct) =>
        SendAsync<ContactMutationResult>(HttpMethod.Post, $"/directory/locations/{locationId}/contacts/{contactId}/remove", JsonContent.Create(request), ct);
    public Task<DirectoryResult<MainContactRetirementResult>> RetireContactAsync(Guid id, RetireMainContactRequest request, CancellationToken ct) =>
        SendAsync<MainContactRetirementResult>(HttpMethod.Post, $"/directory/contacts/{id}/retire", JsonContent.Create(request), ct);
    public Task<DirectoryResult<ContactMutationResult>> LinkContactAsync(Guid locationId, LinkContactRequest request, CancellationToken ct) =>
        SendAsync<ContactMutationResult>(HttpMethod.Post, $"/directory/locations/{locationId}/contacts", JsonContent.Create(request), ct);
    public Task<DirectoryResult<ContactMutationResult>> SetMainContactAsync(Guid locationId, SetMainContactRequest request, CancellationToken ct) =>
        SendAsync<ContactMutationResult>(HttpMethod.Post, $"/directory/locations/{locationId}/main-contact", JsonContent.Create(request), ct);
    public Task<DirectoryResult<ReferenceListViewModel>> TypeListAsync(string key, bool showArchived, CancellationToken ct) =>
        SendAsync<ReferenceListViewModel>(HttpMethod.Get, $"/directory/reference-data/{Uri.EscapeDataString(key)}?showArchived={showArchived}", null, ct);
    public Task<DirectoryResult<ReferenceListItem>> TypeItemAsync(string key, Guid id, CancellationToken ct) =>
        SendAsync<ReferenceListItem>(HttpMethod.Get, $"/directory/reference-data/{Uri.EscapeDataString(key)}/{id}", null, ct);
    public Task<DirectoryResult<DirectoryTypeChoice[]>> LocationTypeChoicesAsync(CancellationToken ct) =>
        SendAsync<DirectoryTypeChoice[]>(HttpMethod.Get, "/directory/reference-data/location-types/choices", null, ct);
    public Task<DirectoryResult<DirectoryTypeChoice>> SaveTypeAsync(string key, Guid? id, SaveDirectoryTypeRequest request, CancellationToken ct) =>
        SendAsync<DirectoryTypeChoice>(id is null ? HttpMethod.Post : HttpMethod.Put,
            $"/directory/reference-data/{Uri.EscapeDataString(key)}" + (id is Guid value ? $"/{value}" : ""), JsonContent.Create(request), ct);
    public Task<DirectoryResult<DirectoryTypeMutationResult>> RetireTypeAsync(string key, Guid id, ReferenceAction action, string? version, CancellationToken ct) =>
        SendAsync<DirectoryTypeMutationResult>(HttpMethod.Post, $"/directory/reference-data/{Uri.EscapeDataString(key)}/{id}/retire",
            JsonContent.Create(new RetireDirectoryTypeRequest(action, version)), ct);
    public Task<DirectoryResult<CustomerSummary[]>> CustomersAsync(CancellationToken ct) =>
        SendAsync<CustomerSummary[]>(HttpMethod.Get, "/directory/customers", null, ct);
    public Task<DirectoryResult<CustomerDetails>> CustomerAsync(Guid id, CancellationToken ct) =>
        SendAsync<CustomerDetails>(HttpMethod.Get, $"/directory/customers/{id}", null, ct);
    public Task<DirectoryResult<CustomerDetails>> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct) =>
        SendAsync<CustomerDetails>(HttpMethod.Post, "/directory/customers", JsonContent.Create(request), ct);
    public Task<DirectoryResult<LocationDetails>> LocationAsync(Guid id, CancellationToken ct) =>
        SendAsync<LocationDetails>(HttpMethod.Get, $"/directory/locations/{id}", null, ct);
    public Task<DirectoryResult<LocationDetails>> AddLocationAsync(Guid customerId, CreateLocationRequest request, CancellationToken ct) =>
        SendAsync<LocationDetails>(HttpMethod.Post, $"/directory/customers/{customerId}/locations", JsonContent.Create(request), ct);
    public Task<DirectoryResult<LocationDetails>> EditLocationAsync(Guid id, EditLocationRequest request, CancellationToken ct) =>
        SendAsync<LocationDetails>(HttpMethod.Put, $"/directory/locations/{id}", JsonContent.Create(request), ct);
    public Task<DirectoryResult<GeographyPage>> PageAsync(Guid? regionId, Guid? countyId, CancellationToken ct, bool showArchived = false)
    {
        List<string> parameters = [];
        if (regionId is Guid region) parameters.Add($"regionId={region}");
        if (countyId is Guid county) parameters.Add($"countyId={county}");
        if (showArchived) parameters.Add("showArchived=true");
        string query = parameters.Count == 0 ? string.Empty : "?" + string.Join('&', parameters);
        return SendAsync<GeographyPage>(HttpMethod.Get, Root + "/" + query, null, ct);
    }
    public Task<DirectoryResult<TownChoice[]>> TownChoicesAsync(CancellationToken ct) =>
        SendAsync<TownChoice[]>(HttpMethod.Get, $"{Root}/town-choices", null, ct);
    public Task<DirectoryResult<TownChoice>> TownReferenceAsync(Guid id, CancellationToken ct) =>
        SendAsync<TownChoice>(HttpMethod.Get, $"{Root}/towns/{id}/reference", null, ct);
    public Task<DirectoryResult<GeographyItem>> FindAsync(string level, Guid id, CancellationToken ct) =>
        SendAsync<GeographyItem>(HttpMethod.Get, $"{Root}/{level}/{id}", null, ct);
    public Task<DirectoryResult<GeographyMutationResult>> RetireAsync(string level, Guid id,
        FieldSales.ReferenceData.ReferenceAction action, string? version, CancellationToken ct) =>
        SendAsync<GeographyMutationResult>(HttpMethod.Post, $"{Root}/{level}/{id}/retire", JsonContent.Create(new RetireGeographyRequest(action, version)), ct);
    public Task<DirectoryResult<GeographyItem>> CreateAsync(string level, string? name, Guid? parentId, CancellationToken ct,
        decimal? latitude = null, decimal? longitude = null) =>
        SendAsync<GeographyItem>(HttpMethod.Post, $"{Root}/{level}", JsonContent.Create(new CreateGeographyRequest(name, parentId, latitude, longitude)), ct);
    public Task<DirectoryResult<GeographyItem>> SetTownCoordinatesAsync(Guid id, SetTownCoordinatesRequest request, CancellationToken ct) =>
        SendAsync<GeographyItem>(HttpMethod.Put, $"{Root}/towns/{id}/coordinates", JsonContent.Create(request), ct);
    public Task<DirectoryResult<GeographyItem>> RenameAsync(string level, Guid id, string? name, string? version, CancellationToken ct) =>
        SendAsync<GeographyItem>(HttpMethod.Put, $"{Root}/{level}/{id}/name", JsonContent.Create(new RenameGeographyRequest(name, version)), ct);
    public Task<DirectoryResult<GeographyImportResult>> ImportAsync(byte[] csv, CancellationToken ct)
    {
        var content = new ByteArrayContent(csv);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return SendAsync<GeographyImportResult>(HttpMethod.Post, $"{Root}/import", content, ct);
    }

    private async Task<DirectoryResult<T>> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using HttpRequestMessage request = new(method, path) { Content = content };
        string? token = contexts.HttpContext is { } context ? await context.GetTokenAsync("access_token") : null;
        if (string.IsNullOrWhiteSpace(token)) return new(HttpStatusCode.Unauthorized);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
                {
                    var error = await response.Content.ReadFromJsonAsync<ApiError>(ct);
                    return new(response.StatusCode, Error: error?.Error ?? "This change could not be saved.",
                        Field: error?.Field, RequiresDuplicateConfirmation: error?.RequiresDuplicateConfirmation == true,
                        Confirmation: error?.Confirmation is { } confirmation && ValidContact(confirmation.Outgoing)
                            && ValidContact(confirmation.Proposed) && confirmation.LocationVersion is not null ? confirmation : null);
                }
                return new(response.StatusCode);
            }
            T? value = await response.Content.ReadFromJsonAsync<T>(ct);
            bool valid = value switch
            {
                null => false,
                GeographyPage page => page.Path is not null && page.Items is not null && page.Level is "regions" or "counties" or "towns"
                    && page.Items.All(item => item is not null && item.Name is not null && item.Version is not null && item.Usage is not null)
                    && page.Path.All(item => item is not null && item.Name is not null),
                GeographyItem item => item.Name is not null && item.Version is not null,
                GeographyMutationResult result => result.Saved,
                DirectoryTypeMutationResult result => result.Saved,
                DirectoryTypeChoice[] types => types.All(ValidType),
                DirectoryTypeChoice type => ValidType(type),
                ReferenceListViewModel list => list.SelectedList is not null && list.AvailableLists is not null && list.Items is not null
                    && list.Items.All(item => item is not null && item.Name is not null && item.Version is not null && item.Usage is not null),
                ReferenceListItem item => item.Name is not null && item.Version is not null && item.Usage is not null,
                CustomerSummary[] customers => customers.All(item => item is not null && item.Name is not null && item.LocationCount >= 1),
                CustomerDetails customer => customer.Name is not null && customer.Version is not null && customer.Locations is { Count: > 0 }
                    && customer.Locations.All(item => item is not null && item.Name is not null && item.Version is not null && ValidTown(item.Town)
                        && (item.Type is null || ValidType(item.Type))),
                LocationDetails location => location.Name is not null && location.CustomerName is not null && location.CustomerId != Guid.Empty
                    && location.Version is not null && ValidTown(location.Town) && (location.Type is null || ValidType(location.Type)),
                ContactChoice[] contacts => contacts.All(ValidContact),
                ContactDetails contact => contact.Id != Guid.Empty && contact.Name is not null && ValidType(contact.Type)
                    && Enum.IsDefined(contact.Status) && contact.Version is not null && contact.Locations is not null
                    && contact.Locations.All(link => link is not null && link.LocationId != Guid.Empty && link.Name is not null
                        && link.CustomerId != Guid.Empty && link.CustomerName is not null && ValidTown(link.Town) && link.LocationVersion is not null),
                ContactLocationChoice[] locations => locations.All(item => item is not null && item.Id != Guid.Empty
                    && item.Name is not null && item.CustomerName is not null && item.TownName is not null),
                LocationContactsPage page => page.LocationId != Guid.Empty && page.LocationName is not null && page.LocationVersion is not null
                    && page.InactiveCount >= 0 && page.Contacts is not null && page.Contacts.All(item => item is not null && ValidContact(item.Contact))
                    && (page.MainContact is null || ValidContact(page.MainContact)),
                ContactMutationResult mutation => mutation.ContactId != Guid.Empty,
                TownChoice[] towns => towns.All(ValidTown),
                TownChoice town => ValidTown(town),
                _ => true
            };
            return valid ? new(response.StatusCode, value) : new(HttpStatusCode.ServiceUnavailable);
        }
        catch (JsonException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (ArgumentException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (NotSupportedException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (HttpRequestException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (Polly.Timeout.TimeoutRejectedException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (Polly.CircuitBreaker.BrokenCircuitException) { return new(HttpStatusCode.ServiceUnavailable); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(HttpStatusCode.ServiceUnavailable); }
    }

    private static bool ValidTown(TownChoice? town) => town is not null && town.Id != Guid.Empty
        && town.Name is not null && town.Label is not null && town.CountyName is not null && town.RegionName is not null;
    private static bool ValidType(DirectoryTypeChoice? type) => type is not null && type.Id != Guid.Empty && type.Name is not null;
    private static bool ValidContact(ContactChoice? contact) => contact is not null && contact.Id != Guid.Empty
        && contact.Name is not null && ValidType(contact.Type) && Enum.IsDefined(contact.Status);
}
