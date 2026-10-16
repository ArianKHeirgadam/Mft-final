using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mft.Api;
using Mft.Application.Abstractions;
using Mft.Domain;
using Mft.Infrastructure.Persistence;

namespace Mft.Api.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
public sealed class CoursesController(AppDbContext db, ICurrentUser current) : ControllerBase
{
    public sealed record CourseRequest(Guid DepartmentId, string Name, string Code, string? Description, bool IsActive = true);

    private async Task<Guid?> ScopeAsync(CancellationToken ct)
    {
        if (current.Role == UserRole.SuperAdmin) return null;
        return await db.Users.Where(u => u.Id == current.UserId).Select(u => u.DepartmentId).SingleOrDefaultAsync(ct);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? departmentId, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        if (scope.HasValue && departmentId.HasValue && departmentId.Value != scope.Value) return Forbid();
        var query = db.Courses.AsNoTracking().Include(c => c.Department).AsQueryable();
        if (scope.HasValue) query = query.Where(c => c.DepartmentId == scope.Value);
        else if (departmentId.HasValue) query = query.Where(c => c.DepartmentId == departmentId.Value);
        return Ok(await query.OrderBy(c => c.Name).Select(c => new
        {
            c.Id, c.DepartmentId, departmentName = c.Department.Name, c.Name, c.Code, c.Description, c.IsActive, c.CreatedAt
        }).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().Include(c => c.Department).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (course is null) return NotFound();
        var scope = await ScopeAsync(ct);
        if (scope.HasValue && scope.Value != course.DepartmentId) return Forbid();
        return Ok(new { course.Id, course.DepartmentId, departmentName = course.Department.Name, course.Name, course.Code, course.Description, course.IsActive, course.CreatedAt });
    }

    [Authorize(Roles = "SuperAdmin,Department")]
    [HttpPost]
    public async Task<IActionResult> Create(CourseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "Name and Code are required." });
        var scope = await ScopeAsync(ct);
        if (scope.HasValue && scope.Value != request.DepartmentId) return Forbid();
        if (!await db.Departments.AnyAsync(d => d.Id == request.DepartmentId && d.IsActive, ct)) return BadRequest(new { message = "Department is missing or inactive." });
        if (await db.Courses.AnyAsync(c => c.DepartmentId == request.DepartmentId && c.Code == request.Code, ct))
            return Conflict(new { message = "A course with this code already exists in this department." });
        var course = new Course { Id = Guid.NewGuid(), DepartmentId = request.DepartmentId, Name = request.Name.Trim(), Code = request.Code.Trim(), Description = request.Description, IsActive = request.IsActive, CreatedAt = DateTime.UtcNow };
        db.Courses.Add(course);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = course.Id }, new { course.Id, course.DepartmentId, course.Name, course.Code, course.Description, course.IsActive, course.CreatedAt });
    }

    [Authorize(Roles = "SuperAdmin,Department")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CourseRequest request, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (course is null) return NotFound();
        var scope = await ScopeAsync(ct);
        if (scope.HasValue && (scope.Value != course.DepartmentId || scope.Value != request.DepartmentId)) return Forbid();
        if (course.DepartmentId != request.DepartmentId) return BadRequest(new { message = "Moving a course between departments is not supported." });
        if (await db.Courses.AnyAsync(c => c.Id != id && c.DepartmentId == request.DepartmentId && c.Code == request.Code, ct))
            return Conflict(new { message = "A course with this code already exists in this department." });
        course.Name = request.Name.Trim(); course.Code = request.Code.Trim(); course.Description = request.Description; course.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize(Roles = "SuperAdmin,Department")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (course is null) return NotFound();
        var scope = await ScopeAsync(ct);
        if (scope.HasValue && scope.Value != course.DepartmentId) return Forbid();
        db.Courses.Remove(course);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
