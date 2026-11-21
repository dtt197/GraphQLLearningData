using System.Net;
using GraphQL.Api.Clients;
using GraphQL.Api.Models;

namespace GraphQL.Api.Services;

// Logic gop du lieu tu Student.Service va Course.Service.
// Tuan 3: cac resolver GraphQL se goi lai class nay.
public sealed class LearningDataService(IStudentClient students, ICourseClient courses)
{
    // Sinh vien + cac dang ky hoc + thong tin tung hoc phan (truy van long nhau, gop tu 2 service)
    public async Task<StudentOverviewDto?> GetStudentOverviewAsync(
        int studentId, string? semester, string? status, CancellationToken ct)
    {
        var studentTask = students.GetStudentAsync(studentId, ct);
        var enrollmentsTask = courses.GetEnrollmentsAsync(studentId, null, semester, status, ct);
        await Task.WhenAll(studentTask, enrollmentsTask);

        var student = await studentTask;
        if (student is null)
        {
            return null;
        }

        var enrollments = await enrollmentsTask;
        var courseList = await courses.GetCoursesByIdsAsync(enrollments.Select(e => e.CourseId), ct);
        var courseMap = courseList.ToDictionary(c => c.Id);

        var details = enrollments
            .Select(e => new EnrollmentDetailDto(
                e.Id, e.Semester, e.Status, e.Grade, courseMap.GetValueOrDefault(e.CourseId)))
            .ToList();

        return new StudentOverviewDto(student, details);
    }

    // Hoc phan + danh sach sinh vien da dang ky
    public async Task<CourseOverviewDto?> GetCourseOverviewAsync(
        int courseId, string? semester, string? status, CancellationToken ct)
    {
        var courseTask = courses.GetCourseAsync(courseId, ct);
        var enrollmentsTask = courses.GetEnrollmentsAsync(null, courseId, semester, status, ct);
        await Task.WhenAll(courseTask, enrollmentsTask);

        var course = await courseTask;
        if (course is null)
        {
            return null;
        }

        var enrollments = await enrollmentsTask;
        var studentList = await students.GetStudentsByIdsAsync(enrollments.Select(e => e.StudentId), ct);
        var studentMap = studentList.ToDictionary(s => s.Id);

        var details = enrollments
            .Select(e => new EnrolledStudentDto(
                e.Id, e.Semester, e.Status, e.Grade, studentMap.GetValueOrDefault(e.StudentId)))
            .ToList();

        return new CourseOverviewDto(course, details);
    }

    // Dang ky hoc phan: kiem tra sinh vien o Student.Service roi moi ghi o Course.Service
    public async Task<EnrollmentDto> EnrollStudentAsync(int studentId, int courseId, CancellationToken ct)
    {
        var student = await students.GetStudentAsync(studentId, ct)
            ?? throw new ServiceException("STUDENT_NOT_FOUND", "Khong tim thay sinh vien.", HttpStatusCode.NotFound);

        if (student.Status != "active")
        {
            throw new ServiceException(
                "STUDENT_NOT_ACTIVE",
                "Chi sinh vien dang hoc (active) moi duoc dang ky hoc phan.",
                HttpStatusCode.Conflict);
        }

        return await courses.EnrollAsync(studentId, courseId, ct);
    }
}