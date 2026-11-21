namespace GraphQL.Api.Models;

// Phan tai du lieu day ve client qua SignalR: chi gom du lieu toi thieu de client cap nhat
public record EnrollmentCreatedMessage(
    int EnrollmentId, int StudentId, int CourseId, string Semester, string Status, DateTimeOffset OccurredAt);

public record StudentStatusChangedMessage(int StudentId, string Status, DateTimeOffset OccurredAt);