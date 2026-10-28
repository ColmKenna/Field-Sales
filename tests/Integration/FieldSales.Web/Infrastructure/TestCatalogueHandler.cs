using System.Collections.Concurrent;
using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using FieldSales.StaffAccess;
using FieldSales.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FieldSales.Web.Tests;

public sealed class TestCatalogueHandler : HttpMessageHandler
{
    private readonly Dictionary<Guid, FieldSales.Catalogue.Contracts.CategoryItem> _categories = [];

    public int Count { get { lock (_categories) return _categories.Count; } }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-access-token", request.Headers.Authorization?.Parameter);
        string path = request.RequestUri!.AbsolutePath.TrimEnd('/');
        if (path == "/staff/session" && request.Method == HttpMethod.Get)
            return Json(HttpStatusCode.OK, new FieldSales.StaffAccess.StaffSessionResponse("staff-1", ["Sales Manager"]));
        if (path == "/catalogue/categories" && request.Method == HttpMethod.Get)
        {
            lock (_categories)
                return Json(HttpStatusCode.OK, _categories.Values.Where(c => c.ParentId is null)
                    .OrderBy(c => c.Name).ToArray());
        }
        if (path == "/catalogue/categories" && request.Method == HttpMethod.Post)
        {
            var body = await request.Content!.ReadFromJsonAsync<CreateRequest>(cancellationToken);
            lock (_categories)
            {
                if (body!.ParentId is not null && !_categories.ContainsKey(body.ParentId.Value))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                if (_categories.Values.Any(c => c.ParentId == body.ParentId &&
                    string.Equals(c.Name, body.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return new HttpResponseMessage(HttpStatusCode.Conflict);
                FieldSales.Catalogue.Contracts.CategoryItem added = new(Guid.NewGuid(), body.ParentId, body.Name.Trim());
                _categories.Add(added.Id, added);
                return Json(HttpStatusCode.Created, added);
            }
        }
        if (path.StartsWith("/catalogue/categories/", StringComparison.Ordinal)
            && path.EndsWith("/name", StringComparison.Ordinal)
            && Guid.TryParse(path["/catalogue/categories/".Length..^"/name".Length], out Guid renameId)
            && request.Method == HttpMethod.Put)
        {
            var body = await request.Content!.ReadFromJsonAsync<RenameRequest>(cancellationToken);
            lock (_categories)
            {
                if (!_categories.TryGetValue(renameId, out var selected))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                if (_categories.Values.Any(c => c.Id != renameId && c.ParentId == selected.ParentId
                    && string.Equals(c.Name, body!.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return new HttpResponseMessage(HttpStatusCode.Conflict);
                var renamed = selected with { Name = body!.Name.Trim() };
                _categories[renameId] = renamed;
                return Json(HttpStatusCode.OK, renamed);
            }
        }
        if (path.StartsWith("/catalogue/categories/", StringComparison.Ordinal)
            && Guid.TryParse(path["/catalogue/categories/".Length..], out Guid id)
            && request.Method == HttpMethod.Get)
        {
            lock (_categories)
            {
                if (!_categories.TryGetValue(id, out var selected))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                List<FieldSales.Catalogue.Contracts.CategoryBreadcrumbSegment> pathSegments = [];
                var current = selected;
                while (true)
                {
                    pathSegments.Add(new(current.Id, current.Name));
                    if (current.ParentId is null) break;
                    current = _categories[current.ParentId.Value];
                }
                pathSegments.Reverse();
                return Json(HttpStatusCode.OK, new FieldSales.Catalogue.Contracts.CategoryDetails(selected,
                    pathSegments, _categories.Values.Where(c => c.ParentId == id).ToArray(), []));
            }
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T value) =>
        new(status) { Content = JsonContent.Create(value) };

    private sealed record CreateRequest(string Name, Guid? ParentId);
    private sealed record RenameRequest(string Name);
}
