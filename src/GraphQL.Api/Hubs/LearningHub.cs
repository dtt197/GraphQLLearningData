using Microsoft.AspNetCore.SignalR;

namespace GraphQL.Api.Hubs;

// Hub: client goi cac phuong thuc Watch/Unwatch de vao/ra nhom nhan thong bao
public sealed class LearningHub(ILogger<LearningHub> logger) : Hub
{
    public async Task WatchStudent(int studentId)
    {
        EnsureValidId(studentId);
        await Groups.AddToGroupAsync(Context.ConnectionId, LearningGroups.ForStudent(studentId));
        logger.LogInformation("Ket noi {ConnectionId} theo doi sinh vien {StudentId}", Context.ConnectionId, studentId);
    }

    public async Task UnwatchStudent(int studentId)
    {
        EnsureValidId(studentId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LearningGroups.ForStudent(studentId));
    }

    public async Task WatchCourse(int courseId)
    {
        EnsureValidId(courseId);
        await Groups.AddToGroupAsync(Context.ConnectionId, LearningGroups.ForCourse(courseId));
        logger.LogInformation("Ket noi {ConnectionId} theo doi hoc phan {CourseId}", Context.ConnectionId, courseId);
    }

    public async Task UnwatchCourse(int courseId)
    {
        EnsureValidId(courseId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LearningGroups.ForCourse(courseId));
    }

    public override Task OnConnectedAsync()
    {
        logger.LogInformation("SignalR: {ConnectionId} da ket noi", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("SignalR: {ConnectionId} da ngat ket noi", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    // HubException: thong diep loi duoc gui ve client
    private static void EnsureValidId(int id)
    {
        if (id <= 0)
        {
            throw new HubException("Ma phai lon hon 0.");
        }
    }
}

// Ten nhom theo mau trong giao trinh (submission:{id})
public static class LearningGroups
{
    public static string ForStudent(int studentId) => $"student:{studentId}";

    public static string ForCourse(int courseId) => $"course:{courseId}";
}