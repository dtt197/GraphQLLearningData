using System.Net;
using System.Text.Json;
using GraphQL.Api.Models;

namespace GraphQL.Api.Clients;

// Lop co so: gui request, phan biet loi ket noi / het thoi gian cho / loi HTTP tu service
public abstract class ServiceClientBase(HttpClient http, ILogger logger, string serviceName)
{
    protected HttpClient Http { get; } = http;

    protected async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Khong ket noi duoc {Service}", serviceName);
            throw new ServiceException(
                "SERVICE_UNAVAILABLE", $"Khong ket noi duoc {serviceName}.", HttpStatusCode.ServiceUnavailable, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "{Service} phan hoi qua cham", serviceName);
            throw new ServiceException(
                "SERVICE_TIMEOUT", $"{serviceName} phan hoi qua cham.", HttpStatusCode.GatewayTimeout, ex);
        }
    }

    protected async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await ToServiceExceptionAsync(response, ct);
        }

        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(ct);
            if (value is not null)
            {
                return value;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Phan than khong dung dinh dang, xu ly o throw ben duoi
        }

        throw new ServiceException(
            "INVALID_RESPONSE", $"{serviceName} tra ve du lieu khong hop le.", HttpStatusCode.BadGateway);
    }

    protected async Task<ServiceException> ToServiceExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ApiError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiError>(ct);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Phan than khong phai ApiError, dung thong tin mac dinh
        }

        logger.LogWarning("{Service} tra ve {StatusCode}: {Code}", serviceName, (int)response.StatusCode, error?.Code);

        return new ServiceException(
            error?.Code ?? "DOWNSTREAM_ERROR",
            error?.Message ?? $"{serviceName} tra ve loi {(int)response.StatusCode}.",
            response.StatusCode);
    }
}