using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Services;
using Xunit;

namespace Qapptia.Core.Tests;

public class HttpUpdateCheckServiceTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsyncWhenRemoteIsHigherReturnsUpdateAvailable()
    {
        var jsonResponse = """
        {
            "version": "2.1.0",
            "releaseDate": "2026-10-01T00:00:00Z",
            "downloadUrl": "https://mi-dominio-ejemplo.com/download",
            "releaseNotes": "Nuevas funciones añadidas",
            "mandatory": false
        }
        """;

        using var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        using var service = new HttpUpdateCheckService(httpClient, currentVersion: "2.0.0");

        var result = await service.CheckForUpdatesAsync(ct: CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.LatestRelease);
        Assert.Equal("2.1.0", result.LatestRelease.Version);
        Assert.Equal("https://mi-dominio-ejemplo.com/download", result.LatestRelease.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsyncWhenRemoteIsSameOrLowerReturnsUpToDate()
    {
        var jsonResponse = """
        {
            "version": "2.0.0",
            "releaseDate": "2026-09-01T00:00:00Z",
            "downloadUrl": "https://mi-dominio-ejemplo.com/download",
            "releaseNotes": "Versión actual",
            "mandatory": false
        }
        """;

        using var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        using var service = new HttpUpdateCheckService(httpClient, currentVersion: "2.0.0");

        var result = await service.CheckForUpdatesAsync(ct: CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.NotNull(result.LatestRelease);
        Assert.Equal("2.0.0", result.LatestRelease.Version);
    }

    [Fact]
    public async Task CheckForUpdatesAsyncWhenHttpErrorReturnsConnectionFailed()
    {
        using var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        using var service = new HttpUpdateCheckService(httpClient, currentVersion: "2.0.0");

        var result = await service.CheckForUpdatesAsync(ct: CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.ConnectionFailed, result.Status);
        Assert.Null(result.LatestRelease);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task CheckForUpdatesAsyncWhenMalformedJsonReturnsInvalidPayload()
    {
        var malformedJson = "{ invalid_json }";

        using var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(malformedJson, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        using var service = new HttpUpdateCheckService(httpClient, currentVersion: "2.0.0");

        var result = await service.CheckForUpdatesAsync(ct: CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.InvalidPayload, result.Status);
    }
}
