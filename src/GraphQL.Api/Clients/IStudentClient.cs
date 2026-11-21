using GraphQL.Api.Models;

namespace GraphQL.Api.Clients;

public interface IStudentClient
{
    Task<IReadOnlyList<StudentDto>> GetStudentsAsync(string? status, CancellationToken ct = default);

    Task<StudentDto?> GetStudentAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<StudentDto>> GetStudentsByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);

    Task<StudentDto> UpdateStatusAsync(int id, string status, CancellationToken ct = default);
}