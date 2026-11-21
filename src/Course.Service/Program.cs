using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CourseDb")
    ?? "Data Source=course.db";

builder.Services.AddDbContext<CourseDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

// Khoi tao DB va du lieu mau
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CourseDbContext>();
    db.Database.EnsureCreated();

    if (!db.Courses.Any())
    {
        db.Courses.AddRange(
            new Course { Code = "IT101", Name = "Lap trinh phan tan", Credits = 3, Semester = "2026-1" },
            new Course { Code = "IT102", Name = "Co so du lieu", Credits = 3, Semester = "2026-1" },
            new Course { Code = "IT103", Name = "Mang may tinh", Credits = 3, Semester = "2026-1" },
            new Course { Code = "IT090", Name = "Cau truc du lieu", Credits = 4, Semester = "2025-2" });
        db.SaveChanges();

        // StudentId chi la tham chieu logic sang Student.Service (khong co khoa ngoai)
        db.Enrollments.AddRange(
            new Enrollment { StudentId = 1, CourseId = 1, Semester = "2026-1", Status = "enrolled" },
            new Enrollment { StudentId = 1, CourseId = 2, Semester = "2026-1", Status = "enrolled" },
            new Enrollment { StudentId = 2, CourseId = 1, Semester = "2026-1", Status = "enrolled" },
            new Enrollment { StudentId = 3, CourseId = 3, Semester = "2026-1", Status = "enrolled" },
            new Enrollment { StudentId = 4, CourseId = 2, Semester = "2026-1", Status = "enrolled" },
            new Enrollment { StudentId = 1, CourseId = 4, Semester = "2025-2", Status = "completed", Grade = 8.5 },
            new Enrollment { StudentId = 2, CourseId = 4, Semester = "2025-2", Status = "completed", Grade = 7.0 });
        db.SaveChanges();
    }
}

// Logging cho moi request
app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    await next();
    stopwatch.Stop();
    app.Logger.LogInformation(
        "{Method} {Path}{Query} -> {StatusCode} ({Elapsed} ms)",
        context.Request.Method,
        context.Request.Path,
        context.Request.QueryString,
        context.Response.StatusCode,
        stopwatch.ElapsedMilliseconds);
});

app.MapGet("/", () => Results.Ok(new { service = "Course.Service", status = "running" }));

app.MapGet("/health", async (CourseDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { service = "Course.Service", status = "healthy" })
        : Results.StatusCode(503);
});

// GET /api/courses?semester=2026-1
app.MapGet("/api/courses", async (string? semester, CourseDbContext db) =>
{
    var query = db.Courses.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(semester))
    {
        var normalized = semester.Trim();
        query = query.Where(c => c.Semester == normalized);
    }

    var courses = await query.OrderBy(c => c.Id).ToListAsync();
    return Results.Ok(courses.Select(CourseDto.From));
});

app.MapGet("/api/courses/{id:int}", async (int id, CourseDbContext db) =>
{
    var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    return course is null
        ? Results.NotFound(new ApiError("COURSE_NOT_FOUND", "Khong tim thay hoc phan."))
        : Results.Ok(CourseDto.From(course));
});

// GET /api/enrollments?studentId=1&courseId=2&semester=2026-1&status=enrolled
app.MapGet("/api/enrollments", async (
    int? studentId, int? courseId, string? semester, string? status, CourseDbContext db) =>
{
    var query = db.Enrollments.AsNoTracking().AsQueryable();

    if (studentId is not null) query = query.Where(e => e.StudentId == studentId);
    if (courseId is not null) query = query.Where(e => e.CourseId == courseId);
    if (!string.IsNullOrWhiteSpace(semester))
    {
        var normalizedSemester = semester.Trim();
        query = query.Where(e => e.Semester == normalizedSemester);
    }
    if (!string.IsNullOrWhiteSpace(status))
    {
        var normalizedStatus = status.Trim().ToLowerInvariant();
        query = query.Where(e => e.Status == normalizedStatus);
    }

    var enrollments = await query.OrderBy(e => e.Id).ToListAsync();
    return Results.Ok(enrollments.Select(EnrollmentDto.From));
});

app.MapGet("/api/enrollments/{id:int}", async (int id, CourseDbContext db) =>
{
    var enrollment = await db.Enrollments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    return enrollment is null
        ? Results.NotFound(new ApiError("ENROLLMENT_NOT_FOUND", "Khong tim thay dang ky hoc."))
        : Results.Ok(EnrollmentDto.From(enrollment));
});

// POST /api/enrollments  (se la mutation enrollStudent o tuan sau)
app.MapPost("/api/enrollments", async (CreateEnrollmentRequest request, CourseDbContext db) =>
{
    if (request.StudentId <= 0 || request.CourseId <= 0)
    {
        return Results.BadRequest(new ApiError("INVALID_ENROLLMENT", "studentId va courseId phai lon hon 0."));
    }

    var course = await db.Courses.FindAsync(request.CourseId);
    if (course is null)
    {
        return Results.NotFound(new ApiError("COURSE_NOT_FOUND", "Khong tim thay hoc phan."));
    }

    var alreadyEnrolled = await db.Enrollments.AnyAsync(e =>
        e.StudentId == request.StudentId && e.CourseId == request.CourseId && e.Status != "dropped");
    if (alreadyEnrolled)
    {
        return Results.Conflict(new ApiError("ALREADY_ENROLLED", "Sinh vien da dang ky hoc phan nay."));
    }

    var enrollment = new Enrollment
    {
        StudentId = request.StudentId,
        CourseId = request.CourseId,
        Semester = course.Semester,
        Status = "enrolled"
    };
    db.Enrollments.Add(enrollment);
    await db.SaveChangesAsync();

    return Results.Created($"/api/enrollments/{enrollment.Id}", EnrollmentDto.From(enrollment));
});

app.Run();

class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Credits { get; set; }
    public string Semester { get; set; } = "";
}

class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public string Semester { get; set; } = "";
    public string Status { get; set; } = "enrolled";
    public double? Grade { get; set; }
}

class CourseDbContext(DbContextOptions<CourseDbContext> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
}

record CourseDto(int Id, string Code, string Name, int Credits, string Semester)
{
    public static CourseDto From(Course c) => new(c.Id, c.Code, c.Name, c.Credits, c.Semester);
}

record EnrollmentDto(int Id, int StudentId, int CourseId, string Semester, string Status, double? Grade)
{
    public static EnrollmentDto From(Enrollment e) =>
        new(e.Id, e.StudentId, e.CourseId, e.Semester, e.Status, e.Grade);
}

record CreateEnrollmentRequest(int StudentId, int CourseId);

record ApiError(string Code, string Message);