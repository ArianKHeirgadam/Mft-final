using Microsoft.EntityFrameworkCore;
using Mft.Domain;
using Mft.Application.Contracts;
namespace Mft.Application.Abstractions;
public interface IMftDbContext { DbSet<User> Users{get;} DbSet<Department> Departments{get;} DbSet<Student> Students{get;} DbSet<Teacher> Teachers{get;} DbSet<Question> Questions{get;} DbSet<QuestionOption> QuestionOptions{get;} DbSet<Exam> Exams{get;} DbSet<ExamQuestion> ExamQuestions{get;} DbSet<ExamAttempt> ExamAttempts{get;} DbSet<ExamAnswer> ExamAnswers{get;} Task<int> SaveChangesAsync(CancellationToken ct); }
public interface ICurrentUser { Guid UserId {get;} UserRole Role {get;} }
public interface ISmsSender { Task SendAsync(string mobile,string message,CancellationToken ct); }
public interface IAuthService { Task<LoginResponse?> LoginAsync(LoginRequest request,CancellationToken ct); Task ChangePasswordAsync(Guid userId,ChangePasswordRequest request,CancellationToken ct); }
public interface IMftService {
 Task<IReadOnlyList<StudentDto>> StudentsAsync(string? search,Guid? departmentId,CancellationToken ct);
 Task<StudentDto> CreateStudentAsync(CreateStudentRequest request,CancellationToken ct);
 Task DeleteStudentAsync(Guid id,CancellationToken ct);
 Task<IReadOnlyList<TeacherDto>> TeachersAsync(string? search,Guid? departmentId,CancellationToken ct);
 Task<TeacherDto> CreateTeacherAsync(CreateTeacherRequest request,CancellationToken ct); Task CreateDepartmentManagerAsync(CreateDepartmentManagerRequest request,CancellationToken ct);
 Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(CancellationToken ct);
 Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentRequest request,CancellationToken ct);
 Task DeleteDepartmentAsync(Guid id,CancellationToken ct);
 Task<IReadOnlyList<QuestionDto>> QuestionsAsync(Guid? departmentId,CancellationToken ct);
 Task<QuestionDto> CreateQuestionAsync(CreateQuestionRequest request,CancellationToken ct);
 Task<IReadOnlyList<ExamDto>> ExamsAsync(CancellationToken ct);
 Task<ExamDto> CreateExamAsync(CreateExamRequest request,CancellationToken ct);
 Task<object> DashboardAsync(CancellationToken ct);
}
