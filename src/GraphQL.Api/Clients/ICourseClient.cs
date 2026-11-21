using GraphQL.Api.Models;

namespace GraphQL.Api.Clients;

public interface ICourseClient
{
    Task<IReadOnlyList<CourseDto>> GetCoursesAsync(string? semester, CancellationToken ct = default);

    Task<CourseDto?> GetCourseAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<CourseDto>> GetCoursesByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);

    Task<IReadOnlyList<EnrollmentDto>> GetEnrollmentsAsync(
        int? studentId, int? courseId, string? semester, string? status, CancellationToken ct = default);

    Task<EnrollmentDto> EnrollAsync(int studentId, int courseId, CancellationToken ct = default);
}