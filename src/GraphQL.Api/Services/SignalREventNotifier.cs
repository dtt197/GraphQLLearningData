using GraphQL.Api.Hubs;
using GraphQL.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace GraphQL.Api.Services;

// Dung IHubContext de day thong bao tu ngoai lop hub (giong UpdateStatusAsync trong bai 3.3)
public sealed class SignalREventNotifier(IHubContext<LearningHub> hub, ILogger<SignalREventNotifier> logger)
    : IEventNotifier
{
    public Task EnrollmentCreatedAsync(EnrollmentDto enrollment, CancellationToken ct = default)
    {
        var message = new EnrollmentCreatedMessage(
            enrollment.Id, enrollment.StudentId, enrollment.CourseId,
            enrollment.Semester, enrollment.Status, DateTimeOffset.UtcNow);

        return SendAsync(
            [LearningGroups.ForStudent(enrollment.StudentId), LearningGroups.ForCourse(enrollment.CourseId)],
            "EnrollmentCreated", message, ct);
    }

    public Task StudentStatusChangedAsync(StudentDto student, CancellationToken ct = default)
    {
        var message = new StudentStatusChangedMessage(student.Id, student.Status, DateTimeOffset.UtcNow);
        return SendAsync([LearningGroups.ForStudent(student.Id)], "StudentStatusChanged", message, ct);
    }

    // Thong bao la phan phu: loi khi gui khong duoc lam hong yeu cau chinh (REST/GraphQL da thanh cong)
    private async Task SendAsync(IReadOnlyList<string> groups, string method, object message, CancellationToken ct)
    {
        try
        {
            await hub.Clients.Groups(groups).SendAsync(method, message, ct);
            logger.LogInformation("Da day thong bao {Method} toi nhom {Groups}", method, string.Join(", ", groups));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Khong day duoc thong bao {Method}", method);
        }
    }
}