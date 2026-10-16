using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mft.Api;
using Mft.Domain;
using Mft.Infrastructure.Persistence;

namespace Mft.Api.Controllers;

[ApiController]
[Route("api/answer-keys")]
[Authorize(Roles = "SuperAdmin,Teacher")]
public sealed class AnswerKeysController(AppDbContext db, ICurrentUser current) : ControllerBase
{
    public sealed record OptionInput(string Text, bool IsCorrect);
    public sealed record AnswerKeyInput(Guid DepartmentId, Guid TeacherId, string Text, QuestionType Type, decimal Points, IReadOnlyList<OptionInput> Options);

    private async Task<bool> OwnsQuestionAsync(Question q, CancellationToken ct)
    {
        if (current.Role == UserRole.SuperAdmin) return true;
        var teacher = await db.Teachers.AsNoTracking().SingleOrDefaultAsync(t => t.Id == q.CreatedByTeacherId, ct);
        return teacher is not null && teacher.UserId == current.UserId;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? departmentId, CancellationToken ct)
    {
        var query = db.Questions.AsNoTracking().Include(q => q.Options).Include(q => q.CreatedByTeacher).AsQueryable();
        if (current.Role == UserRole.Teacher)
        {
            var teacherId = await db.Teachers.Where(t => t.UserId == current.UserId).Select(t => t.Id).SingleOrDefaultAsync(ct);
            if (teacherId == Guid.Empty) return Forbid();
            query = query.Where(q => q.CreatedByTeacherId == teacherId);
        }
        if (departmentId.HasValue) query = query.Where(q => q.DepartmentId == departmentId.Value);
        return Ok(await query.OrderBy(q => q.Text).Select(q => new
        {
            q.Id, q.DepartmentId, q.CreatedByTeacherId, q.Text, type = q.Type.ToString(), q.Points, q.IsActive,
            options = q.Options.Select(o => new { o.Id, o.Text, o.IsCorrect })
        }).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var q = await db.Questions.AsNoTracking().Include(x => x.Options).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) return NotFound();
        if (!await OwnsQuestionAsync(q, ct)) return Forbid();
        return Ok(new { q.Id, q.DepartmentId, q.CreatedByTeacherId, q.Text, type = q.Type.ToString(), q.Points, q.IsActive, options = q.Options.Select(o => new { o.Id, o.Text, o.IsCorrect }) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(AnswerKeyInput request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Points <= 0) return BadRequest(new { message = "Question text and positive points are required." });
        if (request.Type == QuestionType.MultipleChoice && (request.Options.Count < 2 || request.Options.Count(o => o.IsCorrect) != 1))
            return BadRequest(new { message = "Multiple-choice questions require at least two options and exactly one correct option." });
        var teacher = await db.Teachers.SingleOrDefaultAsync(t => t.Id == request.TeacherId && t.DepartmentId == request.DepartmentId, ct);
        if (teacher is null) return BadRequest(new { message = "Teacher must belong to the selected department." });
        if (current.Role == UserRole.Teacher && teacher.UserId != current.UserId) return Forbid();
        var q = new Question { Id = Guid.NewGuid(), DepartmentId = request.DepartmentId, CreatedByTeacherId = request.TeacherId, Text = request.Text.Trim(), Type = request.Type, Points = request.Points, IsActive = true, Options = request.Options.Select(o => new QuestionOption { Id = Guid.NewGuid(), Text = o.Text.Trim(), IsCorrect = o.IsCorrect }).ToList() };
        db.Questions.Add(q);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = q.Id }, new { q.Id, q.DepartmentId, q.CreatedByTeacherId, q.Text, type = q.Type.ToString(), q.Points, q.IsActive, options = q.Options.Select(o => new { o.Id, o.Text, o.IsCorrect }) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, AnswerKeyInput request, CancellationToken ct)
    {
        var q = await db.Questions.Include(x => x.Options).Include(x => x.ExamQuestions).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) return NotFound();
        if (!await OwnsQuestionAsync(q, ct)) return Forbid();
        if (q.ExamQuestions.Count > 0 && await db.ExamAttempts.AnyAsync(a => a.ExamId == q.ExamQuestions.Select(eq => eq.ExamId).FirstOrDefault() && a.SubmittedAt != null, ct))
            return Conflict(new { message = "This question is already used by a submitted exam; editing its answer key is blocked." });
        if (request.DepartmentId != q.DepartmentId || request.TeacherId != q.CreatedByTeacherId)
            return BadRequest(new { message = "Question ownership and department cannot be changed." });
        if (string.IsNullOrWhiteSpace(request.Text) || request.Points <= 0) return BadRequest(new { message = "Question text and positive points are required." });
        if (request.Type == QuestionType.MultipleChoice && (request.Options.Count < 2 || request.Options.Count(o => o.IsCorrect) != 1))
            return BadRequest(new { message = "Multiple-choice questions require at least two options and exactly one correct option." });
        q.Text = request.Text.Trim(); q.Type = request.Type; q.Points = request.Points;
        db.QuestionOptions.RemoveRange(q.Options);
        q.Options = request.Options.Select(o => new QuestionOption { Id = Guid.NewGuid(), QuestionId = q.Id, Text = o.Text.Trim(), IsCorrect = o.IsCorrect }).ToList();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var q = await db.Questions.Include(x => x.Options).Include(x => x.ExamQuestions).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) return NotFound();
        if (!await OwnsQuestionAsync(q, ct)) return Forbid();
        if (q.ExamQuestions.Count > 0) return Conflict(new { message = "Question is linked to an exam and cannot be deleted." });
        db.Questions.Remove(q);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
