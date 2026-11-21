using System.Diagnostics;
using GraphQL.Api.Clients;
using GraphQL.Api.Models;
using GraphQL.Api.Services;

var builder = WebApplication.CreateBuilder(args);

string GetServiceUrl(string key) =>
    builder.Configuration[key] ?? throw new InvalidOperationException($"Thieu cau hinh {key}");

// Dia chi service doc tu cau hinh; trong Docker ghi de bang bien moi truong Services__StudentUrl, Services__CourseUrl
builder.Services.AddHttpClient<IStudentClient, HttpStudentClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("Services:StudentUrl"));
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient<ICourseClient, HttpCourseClient>(client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("Services:CourseUrl"));
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<LearningDataService>();

var app = builder.Build();

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

// Anh xa loi tu service noi bo thanh phan hoi HTTP thong nhat
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (ServiceException ex)
    {
        app.Logger.LogWarning("Loi service: {Code} - {Message}", ex.Code, ex.Message);
        context.Response.StatusCode = ex.ToHttpStatus();
        await context.Response.WriteAsJsonAsync(new ApiError(ex.Code, ex.Message));
    }
});

app.MapGet("/", () => Results.Ok(new { service = "GraphQL.Api", status = "running" }));

app.MapGet("/health", () => Results.Ok(new { service = "GraphQL.Api", status = "healthy" }));

// ---- Endpoint REST tam de kiem thu logic gop du lieu (tuan 3 thay bang POST /graphql) ----

app.MapGet("/api/students", async (string? status, IStudentClient students, CancellationToken ct) =>
    Results.Ok(await students.GetStudentsAsync(status, ct)));

app.MapGet("/api/courses", async (string? semester, ICourseClient courses, CancellationToken ct) =>
    Results.Ok(await courses.GetCoursesAsync(semester, ct)));

app.MapGet("/api/students/{id:int}/overview", async (
    int id, string? semester, string? status, LearningDataService service, CancellationToken ct) =>
{
    var overview = await service.GetStudentOverviewAsync(id, semester, status, ct);
    return overview is null
        ? Results.NotFound(new ApiError("STUDENT_NOT_FOUND", "Khong tim thay sinh vien."))
        : Results.Ok(overview);
});

app.MapGet("/api/courses/{id:int}/overview", async (
    int id, string? semester, string? status, LearningDataService service, CancellationToken ct) =>
{
    var overview = await service.GetCourseOverviewAsync(id, semester, status, ct);
    return overview is null
        ? Results.NotFound(new ApiError("COURSE_NOT_FOUND", "Khong tim thay hoc phan."))
        : Results.Ok(overview);
});

app.MapPatch("/api/students/{id:int}/status", async (
    int id, UpdateStudentStatusRequest request, IStudentClient students, CancellationToken ct) =>
    Results.Ok(await students.UpdateStatusAsync(id, request.Status ?? "", ct)));

app.MapPost("/api/enrollments", async (
    EnrollStudentRequest request, LearningDataService service, CancellationToken ct) =>
{
    var enrollment = await service.EnrollStudentAsync(request.StudentId, request.CourseId, ct);
    return Results.Json(enrollment, statusCode: StatusCodes.Status201Created);
});

app.Run();