using System.Net;
using GraphQL.Api.Models;

namespace GraphQL.Api.Clients;

public sealed class HttpStudentClient(HttpClient http, ILogger<HttpStudentClient> logger)
    : ServiceClientBase(http, logger, "Student.Service"), IStudentClient
{
    public async Task<IReadOnlyList<StudentDto>> GetStudentsAsync(string? status, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(status)
            ? "/api/students"
            : $"/api/students?status={Uri.EscapeDataString(status)}";

        using var response = await SendAsync(() => Http.GetAsync(url, ct), ct);
        return await ReadAsync<List<StudentDto>>(response, ct);
    }

    public async Task<StudentDto?> GetStudentAsync(int id, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => Http.GetAsync($"/api/students/{id}", ct), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadAsync<StudentDto>(response, ct);
    }

    public async Task<IReadOnlyList<StudentDto>> GetStudentsByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        var url = $"/api/students/by-ids?ids={string.Join(",", idList)}";
        using var response = await SendAsync(() => Http.GetAsync(url, ct), ct);
        return await ReadAsync<List<StudentDto>>(response, ct);
    }

    public async Task<StudentDto> UpdateStatusAsync(int id, string status, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            () => Http.PatchAsJsonAsync($"/api/students/{id}/status", new { status }, ct), ct);
        return await ReadAsync<StudentDto>(response, ct);
    }
}