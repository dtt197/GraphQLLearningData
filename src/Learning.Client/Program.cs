using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

var gatewayUrl = (args.Length > 0 ? args[0] : "http://localhost:5100").TrimEnd('/');

using var http = new HttpClient
{
    BaseAddress = new Uri(gatewayUrl),
    Timeout = TimeSpan.FromSeconds(10)
};

await using var connection = new HubConnectionBuilder()
    .WithUrl($"{gatewayUrl}/hubs/learning")
    .WithAutomaticReconnect()
    .Build();

// Cac ma dang theo doi: dung de vao lai nhom sau khi ket noi lai
var watchedStudents = new HashSet<int>();
var watchedCourses = new HashSet<int>();

// Dang ky ham xu ly: ten phai khop chinh xac voi ten phuong thuc gateway gui (SendAsync)
connection.On<EnrollmentCreatedMessage>("EnrollmentCreated", message =>
    Console.WriteLine(
        $"\n[Thong bao] Dang ky moi #{message.EnrollmentId}: sinh vien {message.StudentId} -> " +
        $"hoc phan {message.CourseId} ({message.Semester}, {message.Status}) luc {message.OccurredAt.ToLocalTime():HH:mm:ss}"));

connection.On<StudentStatusChangedMessage>("StudentStatusChanged", message =>
    Console.WriteLine(
        $"\n[Thong bao] Sinh vien {message.StudentId} doi trang thai -> {message.Status} " +
        $"luc {message.OccurredAt.ToLocalTime():HH:mm:ss}"));

connection.Reconnecting += _ =>
{
    Console.WriteLine("\n[Ket noi] Mat ket noi, dang thu ket noi lai...");
    return Task.CompletedTask;
};

connection.Reconnected += _ =>
{
    Console.WriteLine("\n[Ket noi] Da ket noi lai.");
    return RestoreAsync(connection, http, watchedStudents, watchedCourses);
};

connection.Closed += _ =>
{
    Console.WriteLine("\n[Ket noi] Ket noi da dong. Go 'connect' de ket noi lai.");
    return Task.CompletedTask;
};

try
{
    await connection.StartAsync();
    Console.WriteLine($"Da ket noi SignalR toi {gatewayUrl}/hubs/learning");
}
catch (Exception ex)
{
    // Khong gia dinh gateway luon san sang: cac lenh REST van dung duoc
    Console.WriteLine($"Khong ket noi duoc SignalR ({ex.Message}). Go 'connect' de thu lai.");
}

PrintHelp();

while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine();
    if (line is null)
    {
        break;
    }

    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
    {
        continue;
    }

    var command = parts[0].ToLowerInvariant();
    if (command is "quit" or "exit")
    {
        break;
    }

    try
    {
        switch (command)
        {
            case "help": PrintHelp(); break;
            case "students": await CmdStudentsAsync(parts, http); break;
            case "student": await CmdStudentAsync(parts, http); break;
            case "course": await CmdCourseAsync(parts, http); break;
            case "watch": await CmdWatchAsync(parts, connection, watchedStudents, watchedCourses); break;
            case "enroll": await CmdEnrollAsync(parts, http); break;
            case "status": await CmdStatusAsync(parts, http); break;
            case "connect": await CmdConnectAsync(connection, http, watchedStudents, watchedCourses); break;
            default: Console.WriteLine("Lenh khong hop le. Go 'help'."); break;
        }
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"[Loi mang] Khong goi duoc gateway: {ex.Message}");
    }
    catch (TaskCanceledException)
    {
        Console.WriteLine("[Loi] Gateway phan hoi qua cham (het thoi gian cho).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Loi] {ex.Message}");
    }
}

static void PrintHelp()
{
    Console.WriteLine("""

    Cac lenh:
      students [trangThai]        Danh sach sinh vien (active, suspended, graduated)
      student <id> [hocKy]        Sinh vien + dang ky hoc + hoc phan (gop tu 2 service)
      course <id>                 Hoc phan + sinh vien da dang ky
      watch student <id>          Theo doi thong bao cua mot sinh vien
      watch course <id>           Theo doi thong bao cua mot hoc phan
      enroll <maSV> <maHP>        Dang ky hoc phan
      status <maSV> <trangThai>   Doi trang thai sinh vien
      connect                     Ket noi lai SignalR
      help | quit
    """);
}

static async Task CmdStudentsAsync(string[] parts, HttpClient http)
{
    var url = parts.Length > 1 ? $"/api/students?status={Uri.EscapeDataString(parts[1])}" : "/api/students";
    using var response = await http.GetAsync(url);
    if (await PrintIfErrorAsync(response))
    {
        return;
    }

    var students = await response.Content.ReadFromJsonAsync<List<StudentDto>>() ?? [];
    foreach (var s in students)
    {
        Console.WriteLine($"  [{s.Id}] {s.Code} - {s.FullName} ({s.Status})");
    }
}

static async Task CmdStudentAsync(string[] parts, HttpClient http)
{
    if (parts.Length < 2 || !int.TryParse(parts[1], out var studentId))
    {
        Console.WriteLine("Cu phap: student <id> [hocKy]");
        return;
    }

    await ShowStudentAsync(http, studentId, parts.Length > 2 ? parts[2] : null);
}

static async Task CmdCourseAsync(string[] parts, HttpClient http)
{
    if (parts.Length < 2 || !int.TryParse(parts[1], out var courseId))
    {
        Console.WriteLine("Cu phap: course <id>");
        return;
    }

    using var response = await http.GetAsync($"/api/courses/{courseId}/overview");
    if (await PrintIfErrorAsync(response))
    {
        return;
    }

    var overview = await response.Content.ReadFromJsonAsync<CourseOverviewDto>();
    if (overview is null)
    {
        return;
    }

    Console.WriteLine($"{overview.Course.Code} - {overview.Course.Name} ({overview.Course.Credits} tin chi, {overview.Course.Semester})");
    foreach (var e in overview.Students)
    {
        Console.WriteLine($"  - {e.Student?.Code} {e.Student?.FullName} | {e.Semester} | {e.Status} | diem: {e.Grade?.ToString() ?? "-"}");
    }
}

static async Task CmdWatchAsync(
    string[] parts, HubConnection connection, HashSet<int> watchedStudents, HashSet<int> watchedCourses)
{
    if (parts.Length < 3 || !int.TryParse(parts[2], out var id))
    {
        Console.WriteLine("Cu phap: watch student <id>  hoac  watch course <id>");
        return;
    }

    if (connection.State != HubConnectionState.Connected)
    {
        Console.WriteLine($"Chua ket noi SignalR (trang thai: {connection.State}). Go 'connect'.");
        return;
    }

    switch (parts[1].ToLowerInvariant())
    {
        case "student":
            await connection.InvokeAsync("WatchStudent", id);
            watchedStudents.Add(id);
            Console.WriteLine($"Dang theo doi sinh vien {id}.");
            break;
        case "course":
            await connection.InvokeAsync("WatchCourse", id);
            watchedCourses.Add(id);
            Console.WriteLine($"Dang theo doi hoc phan {id}.");
            break;
        default:
            Console.WriteLine("Cu phap: watch student <id>  hoac  watch course <id>");
            break;
    }
}

static async Task CmdEnrollAsync(string[] parts, HttpClient http)
{
    if (parts.Length < 3 || !int.TryParse(parts[1], out var studentId) || !int.TryParse(parts[2], out var courseId))
    {
        Console.WriteLine("Cu phap: enroll <maSV> <maHP>");
        return;
    }

    using var response = await http.PostAsJsonAsync("/api/enrollments", new { studentId, courseId });
    if (await PrintIfErrorAsync(response))
    {
        return;
    }

    var enrollment = await response.Content.ReadFromJsonAsync<EnrollmentDto>();
    Console.WriteLine($"Da dang ky: #{enrollment?.Id} sinh vien {studentId} -> hoc phan {courseId} ({enrollment?.Semester})");
}

static async Task CmdStatusAsync(string[] parts, HttpClient http)
{
    if (parts.Length < 3 || !int.TryParse(parts[1], out var studentId))
    {
        Console.WriteLine("Cu phap: status <maSV> <active|suspended|graduated>");
        return;
    }

    using var response = await http.PatchAsJsonAsync($"/api/students/{studentId}/status", new { status = parts[2] });
    if (await PrintIfErrorAsync(response))
    {
        return;
    }

    var student = await response.Content.ReadFromJsonAsync<StudentDto>();
    Console.WriteLine($"Da cap nhat: {student?.Code} -> {student?.Status}");
}

static async Task CmdConnectAsync(
    HubConnection connection, HttpClient http, HashSet<int> watchedStudents, HashSet<int> watchedCourses)
{
    if (connection.State != HubConnectionState.Disconnected)
    {
        Console.WriteLine($"Trang thai hien tai: {connection.State}");
        return;
    }

    await connection.StartAsync();
    Console.WriteLine("Da ket noi SignalR.");
    await RestoreAsync(connection, http, watchedStudents, watchedCourses);
}

// Sau khi ket noi lai: ConnectionId doi nen nhom bi mat -> vao lai nhom, roi dong bo trang thai bang REST
// (kenh thoi gian thuc khong phai nguon su that duy nhat, nhu giao trinh chuong 3)
static async Task RestoreAsync(
    HubConnection connection, HttpClient http, HashSet<int> watchedStudents, HashSet<int> watchedCourses)
{
    try
    {
        var students = watchedStudents.ToList();
        var courses = watchedCourses.ToList();

        foreach (var id in students)
        {
            await connection.InvokeAsync("WatchStudent", id);
        }

        foreach (var id in courses)
        {
            await connection.InvokeAsync("WatchCourse", id);
        }

        if (students.Count + courses.Count > 0)
        {
            Console.WriteLine("[Ket noi] Da theo doi lai cac ma truoc do, dong bo trang thai bang REST:");
        }

        foreach (var id in students)
        {
            await ShowStudentAsync(http, id, null);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Ket noi] Khong khoi phuc duoc theo doi: {ex.Message}");
    }
}

static async Task ShowStudentAsync(HttpClient http, int studentId, string? semester)
{
    var url = $"/api/students/{studentId}/overview";
    if (!string.IsNullOrWhiteSpace(semester))
    {
        url += $"?semester={Uri.EscapeDataString(semester)}";
    }

    using var response = await http.GetAsync(url);
    if (await PrintIfErrorAsync(response))
    {
        return;
    }

    var overview = await response.Content.ReadFromJsonAsync<StudentOverviewDto>();
    if (overview is null)
    {
        return;
    }

    Console.WriteLine($"{overview.Student.Code} - {overview.Student.FullName} ({overview.Student.Status})");
    foreach (var e in overview.Enrollments)
    {
        Console.WriteLine($"  - {e.Course?.Code} {e.Course?.Name} | {e.Semester} | {e.Status} | diem: {e.Grade?.ToString() ?? "-"}");
    }
}

// Doc ApiError khi gateway tra loi khong thanh cong (404, 409, 503...)
static async Task<bool> PrintIfErrorAsync(HttpResponseMessage response)
{
    if (response.IsSuccessStatusCode)
    {
        return false;
    }

    ApiError? error = null;
    try
    {
        error = await response.Content.ReadFromJsonAsync<ApiError>();
    }
    catch (Exception ex) when (ex is JsonException or NotSupportedException)
    {
        // Phan than khong phai ApiError
    }

    Console.WriteLine($"Loi {(int)response.StatusCode}: {error?.Code} - {error?.Message}");
    return true;
}

record StudentDto(int Id, string Code, string FullName, string Email, string Status);

record CourseDto(int Id, string Code, string Name, int Credits, string Semester);

record EnrollmentDto(int Id, int StudentId, int CourseId, string Semester, string Status, double? Grade);

record EnrollmentDetailDto(int EnrollmentId, string Semester, string Status, double? Grade, CourseDto? Course);

record StudentOverviewDto(StudentDto Student, List<EnrollmentDetailDto> Enrollments);

record EnrolledStudentDto(int EnrollmentId, string Semester, string Status, double? Grade, StudentDto? Student);

record CourseOverviewDto(CourseDto Course, List<EnrolledStudentDto> Students);

record EnrollmentCreatedMessage(
    int EnrollmentId, int StudentId, int CourseId, string Semester, string Status, DateTimeOffset OccurredAt);

record StudentStatusChangedMessage(int StudentId, string Status, DateTimeOffset OccurredAt);

record ApiError(string Code, string Message);