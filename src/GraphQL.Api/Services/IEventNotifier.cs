using GraphQL.Api.Models;

namespace GraphQL.Api.Services;

public interface IEventNotifier
{
    Task EnrollmentCreatedAsync(EnrollmentDto enrollment, CancellationToken ct = default);

    Task StudentStatusChangedAsync(StudentDto student, CancellationToken ct = default);
}