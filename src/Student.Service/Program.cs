using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("StudentDb")
    ?? "Data Source=student.db";

builder.Services.AddDbContext<StudentDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

// Khoi tao DB va du lieu mau
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudentDbContext>();
    db.Database.EnsureCreated();

    if (!db.Students.Any())
    {
        db.Students.AddRange(
            new Student { Code = "SV001", FullName = "Nguyen Van An", Email = "an.nv@example.edu", Status = "active" },
            new Student { Code = "SV002", FullName = "Tran Thi Binh", Email = "binh.tt@example.edu", Status = "active" },
            new Student { Code = "SV003", FullName = "Le Van Cuong", Email = "cuong.lv@example.edu", Status = "suspended" },
            new Student { Code = "SV004", FullName = "Pham Thi Dung", Email = "dung.pt@example.edu", Status = "active" },
            new Student { Code = "SV005", FullName = "Hoang Van Em", Email = "em.hv@example.edu", Status = "graduated" });
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

string[] validStatuses = ["active", "suspended", "graduated"];

app.MapGet("/", () => Results.Ok(new { service = "Student.Service", status = "running" }));

app.MapGet("/health", async (StudentDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { service = "Student.Service", status = "healthy" })
        : Results.StatusCode(503);
});

// GET /api/students?status=active
app.MapGet("/api/students", async (string? status, StudentDbContext db) =>
{
    var query = db.Students.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(status))
    {
        var normalized = status.Trim().ToLowerInvariant();
        query = query.Where(s => s.Status == normalized);
    }

    var students = await query.OrderBy(s => s.Id).ToListAsync();
    return Results.Ok(students.Select(StudentDto.From));
});

// GET /api/students/by-ids?ids=1,2,3  (gateway se dung de gop du lieu)
app.MapGet("/api/students/by-ids", async (string ids, StudentDbContext db) =>
{
    var parsed = ids
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => int.TryParse(x, out var value) ? value : (int?)null)
        .ToList();

    if (parsed.Count == 0 || parsed.Any(x => x is null))
    {
        return Results.BadRequest(new ApiError("INVALID_IDS", "ids phai la danh sach so nguyen, vi du 1,2,3."));
    }

    var wanted = parsed.Select(x => x!.Value).Distinct().ToList();
    var students = await db.Students.AsNoTracking()
        .Where(s => wanted.Contains(s.Id))
        .OrderBy(s => s.Id)
        .ToListAsync();

    return Results.Ok(students.Select(StudentDto.From));
});

app.MapGet("/api/students/{id:int}", async (int id, StudentDbContext db) =>
{
    var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    return student is null
        ? Results.NotFound(new ApiError("STUDENT_NOT_FOUND", "Khong tim thay sinh vien."))
        : Results.Ok(StudentDto.From(student));
});

// PATCH /api/students/{id}/status  (se tro thanh mutation o tuan sau)
app.MapPatch("/api/students/{id:int}/status", async (int id, UpdateStudentStatusRequest request, StudentDbContext db) =>
{
    var status = request.Status?.Trim().ToLowerInvariant();
    if (status is null || !validStatuses.Contains(status))
    {
        return Results.BadRequest(new ApiError("INVALID_STATUS", "Trang thai phai la active, suspended hoac graduated."));
    }

    var student = await db.Students.FindAsync(id);
    if (student is null)
    {
        return Results.NotFound(new ApiError("STUDENT_NOT_FOUND", "Khong tim thay sinh vien."));
    }

    student.Status = status;
    await db.SaveChangesAsync();
    return Results.Ok(StudentDto.From(student));
});

app.Run();

class Student
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Status { get; set; } = "active";
}

class StudentDbContext(DbContextOptions<StudentDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
}

record StudentDto(int Id, string Code, string FullName, string Email, string Status)
{
    public static StudentDto From(Student s) => new(s.Id, s.Code, s.FullName, s.Email, s.Status);
}

record UpdateStudentStatusRequest(string? Status);

record ApiError(string Code, string Message);