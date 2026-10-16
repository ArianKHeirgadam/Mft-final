using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mft.Application.Abstractions;
using Mft.Application.Contracts;
using Mft.Domain;
using Mft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Mft.Api.Controllers;
[ApiController][Route("api")][Authorize] public sealed class MftController(IMftService s,AppDbContext db):ControllerBase{
 public sealed record UpdateDepartmentRequest(string Name,string? ManagerName,string DegreeLevel,bool IsActive);
 [Authorize(Roles="SuperAdmin,Department,Teacher,Student")][HttpGet("students")] public Task<IReadOnlyList<StudentDto>> Students(string? search,Guid? departmentId,CancellationToken ct)=>s.StudentsAsync(search,departmentId,ct);
 [Authorize(Roles="SuperAdmin,Department,Teacher")][HttpPost("students")] public Task<StudentDto> CreateStudent(CreateStudentRequest r,CancellationToken ct)=>s.CreateStudentAsync(r,ct);
 [Authorize(Roles="SuperAdmin,Department,Teacher")][HttpDelete("students/{id:guid}")] public async Task<IActionResult> DeleteStudent(Guid id,CancellationToken ct){await s.DeleteStudentAsync(id,ct);return NoContent();}
 [Authorize(Roles="SuperAdmin,Department,Teacher")][HttpGet("teachers")] public Task<IReadOnlyList<TeacherDto>> Teachers(string? search,Guid? departmentId,CancellationToken ct)=>s.TeachersAsync(search,departmentId,ct);
 [Authorize(Roles="SuperAdmin,Department")][HttpPost("teachers")] public Task<TeacherDto> CreateTeacher(CreateTeacherRequest r,CancellationToken ct)=>s.CreateTeacherAsync(r,ct);
 [Authorize(Roles="SuperAdmin")][HttpPost("department-managers")] public async Task<IActionResult> CreateDepartmentManager(CreateDepartmentManagerRequest r,CancellationToken ct){var temporaryPassword=await s.CreateDepartmentManagerAsync(r,ct);return Ok(new{message="مدیر دپارتمان ایجاد شد؛ تغییر رمز در اولین ورود الزامی است.",temporaryPassword});}
 [Authorize(Roles="SuperAdmin,Department,Teacher,Student")][HttpGet("departments")] public Task<IReadOnlyList<DepartmentDto>> Departments(CancellationToken ct)=>s.DepartmentsAsync(ct);
 [Authorize(Roles="SuperAdmin")][HttpPost("departments")] public Task<DepartmentDto> CreateDepartment(CreateDepartmentRequest r,CancellationToken ct)=>s.CreateDepartmentAsync(r,ct);
 [Authorize(Roles="SuperAdmin")][HttpPut("departments/{id:guid}")] public async Task<IActionResult> UpdateDepartment(Guid id,UpdateDepartmentRequest r,CancellationToken ct){var d=await db.Departments.SingleOrDefaultAsync(x=>x.Id==id,ct);if(d is null)return NotFound();if(string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.DegreeLevel))return BadRequest(new{message="نام و مقطع دپارتمان الزامی است."});if(await db.Departments.AnyAsync(x=>x.Id!=id&&x.Name==r.Name,ct))return Conflict(new{message="دپارتمان دیگری با این نام وجود دارد."});d.Name=r.Name.Trim();d.ManagerName=r.ManagerName;d.DegreeLevel=r.DegreeLevel.Trim();d.IsActive=r.IsActive;await db.SaveChangesAsync(ct);return NoContent();}
 [Authorize(Roles="SuperAdmin")][HttpDelete("departments/{id:guid}")] public async Task<IActionResult> DeleteDepartment(Guid id,CancellationToken ct){var d=await db.Departments.SingleOrDefaultAsync(x=>x.Id==id,ct);if(d is null)return NotFound();if(await db.Users.AnyAsync(x=>x.DepartmentId==id,ct)||await db.Students.AnyAsync(x=>x.DepartmentId==id,ct)||await db.Teachers.AnyAsync(x=>x.DepartmentId==id,ct)||await db.Questions.AnyAsync(x=>x.DepartmentId==id,ct)||await db.Exams.AnyAsync(x=>x.DepartmentId==id,ct)||await db.Courses.AnyAsync(x=>x.DepartmentId==id,ct))return Conflict(new{message="این دپارتمان دارای کاربر یا اطلاعات وابسته است و قابل حذف نیست."});await s.DeleteDepartmentAsync(id,ct);return NoContent();}
 [Authorize(Roles="SuperAdmin,Department,Teacher")][HttpGet("questions")] public Task<IReadOnlyList<QuestionDto>> Questions(Guid? departmentId,CancellationToken ct)=>s.QuestionsAsync(departmentId,ct);
 [Authorize(Roles="SuperAdmin,Teacher")][HttpPost("questions")] public Task<QuestionDto> CreateQuestion(CreateQuestionRequest r,CancellationToken ct)=>s.CreateQuestionAsync(r,ct);
 [Authorize(Roles="SuperAdmin,Department,Teacher")][HttpGet("exams")] public Task<IReadOnlyList<ExamDto>> Exams(CancellationToken ct)=>s.ExamsAsync(ct);
 [Authorize(Roles="SuperAdmin,Department,Teacher")][HttpPost("exams")] public Task<ExamDto> CreateExam(CreateExamRequest r,CancellationToken ct)=>s.CreateExamAsync(r,ct);
 [Authorize(Roles="SuperAdmin,Department,Teacher,Student")][HttpGet("dashboard")] public Task<object> Dashboard(CancellationToken ct)=>s.DashboardAsync(ct);
}