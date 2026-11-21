using System.Net;
using GraphQL.Api.Models;

namespace GraphQL.Api.Clients;

public sealed class HttpCourseClient(HttpClient http, ILogger<HttpCourseClient> logger)
    : ServiceClientBase(http, logger, "Course.Service"), ICourseClient
{
    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(string? semester, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(semester)
            ? "/api/courses"
            : $"/api/courses?semester={Uri.EscapeDataString(semester)}";

        using var response = await SendAsync(() => Http.GetAsync(url, ct), ct);
        return await ReadAsync<List<CourseDto>>(response, ct);
    }

    public async Task<CourseDto?> GetCourseAsync(int id, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => Http.GetAsync($"/api/courses/{id}", ct), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadAsync<CourseDto>(response, ct);
    }

    public async Task<IReadOnlyList<CourseDto>> GetCoursesByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        var url = $"/api/courses/by-ids?ids={string.Join(",", idList)}";
        using var response = await SendAsync(() => Http.GetAsync(url, ct), ct);
        return await ReadAsync<List<CourseDto>>(response, ct);
    }

    public async Task<IReadOnlyList<EnrollmentDto>> GetEnrollmentsAsync(
        int? studentId, int? courseId, string? semester, string? status, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (studentId is not null) query.Add($"studentId={studentId}");
        if (courseId is not null) query.Add($"courseId={courseId}");
        if (!string.IsNullOrWhiteSpace(semester)) query.Add($"semester={Uri.EscapeDataString(semester)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");

        var url = "/api/enrollments" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        using var response = await SendAsync(() => Http.GetAsync(url, ct), ct);
        return await ReadAsync<List<EnrollmentDto>>(response, ct);
    }

    public async Task<EnrollmentDto> EnrollAsync(int studentId, int courseId, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            () => Http.PostAsJsonAsync("/api/enrollments", new { studentId, courseId }, ct), ct);
        return await ReadAsync<EnrollmentDto>(response, ct);
    }
}