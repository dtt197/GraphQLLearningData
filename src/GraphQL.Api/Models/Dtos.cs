namespace GraphQL.Api.Models;

// DTO nhan tu Student.Service va Course.Service
public record StudentDto(int Id, string Code, string FullName, string Email, string Status);

public record CourseDto(int Id, string Code, string Name, int Credits, string Semester);

public record EnrollmentDto(int Id, int StudentId, int CourseId, string Semester, string Status, double? Grade);

// DTO tong hop du lieu tu ca hai service
public record EnrollmentDetailDto(int EnrollmentId, string Semester, string Status, double? Grade, CourseDto? Course);

public record StudentOverviewDto(StudentDto Student, IReadOnlyList<EnrollmentDetailDto> Enrollments);

public record EnrolledStudentDto(int EnrollmentId, string Semester, string Status, double? Grade, StudentDto? Student);

public record CourseOverviewDto(CourseDto Course, IReadOnlyList<EnrolledStudentDto> Students);

// DTO request cua gateway
public record UpdateStudentStatusRequest(string? Status);

public record EnrollStudentRequest(int StudentId, int CourseId);

public record ApiError(string Code, string Message);