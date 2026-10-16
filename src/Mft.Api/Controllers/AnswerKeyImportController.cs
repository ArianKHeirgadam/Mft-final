using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mft.Api;
using Mft.Application.Abstractions;
using Mft.Domain;
using Mft.Infrastructure.Persistence;
using UglyToad.PdfPig;

namespace Mft.Api.Controllers;

[ApiController]
[Route("api/answer-keys/import")]
[Authorize(Roles = "SuperAdmin,Teacher")]
public sealed class AnswerKeyImportController(AppDbContext db, ICurrentUser current) : ControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024;
    public sealed class ImportRequest
    {
        public Guid DepartmentId { get; set; }
        public Guid TeacherId { get; set; }
        public IFormFile? File { get; set; }
    }
    private sealed record ImportedOption(string Text, bool IsCorrect);
    private sealed record ImportedQuestion(string Text, QuestionType Type, decimal Points, List<ImportedOption> Options);

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Import([FromForm] ImportRequest request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0) return BadRequest(new { message = "Select a non-empty answer-key file." });
        if (request.File.Length > MaxBytes) return BadRequest(new { message = "Maximum upload size is 10 MB." });
        var teacher = await db.Teachers.SingleOrDefaultAsync(t => t.Id == request.TeacherId && t.DepartmentId == request.DepartmentId, ct);
        if (teacher is null) return BadRequest(new { message = "Teacher must belong to the selected department." });
        if (current.Role == UserRole.Teacher && teacher.UserId != current.UserId) return Forbid();

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        List<ImportedQuestion> questions;
        await using var stream = request.File.OpenReadStream();
        try
        {
            questions = extension switch
            {
                ".json" => await ParseJsonAsync(stream, ct),
                ".csv" => await ParseDelimitedAsync(stream, ',', ct),
                ".tsv" => await ParseDelimitedAsync(stream, '\t', ct),
                ".txt" => await ParseTextAsync(stream, ct),
                ".xml" => await ParseXmlAsync(stream, ct),
                ".xlsx" or ".xlsm" => ParseExcel(stream),
                ".pdf" => ParsePdf(stream),
                _ => throw new FormatException("Supported formats: .xlsx, .xlsm, .pdf, .csv, .tsv, .json, .xml, .txt.")
            };
        }
        catch (Exception ex) when (ex is FormatException or JsonException or XmlException or InvalidDataException)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (questions.Count == 0) return BadRequest(new { message = "No questions were found in the uploaded file." });
        if (questions.Count > 500) return BadRequest(new { message = "A single import is limited to 500 questions." });
        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            if (string.IsNullOrWhiteSpace(q.Text) || q.Points <= 0)
                return BadRequest(new { message = $"Question {i + 1} requires text and a positive point value." });
            if (q.Type == QuestionType.MultipleChoice && (q.Options.Count < 2 || q.Options.Count(o => o.IsCorrect) != 1))
                return BadRequest(new { message = $"Question {i + 1} must have at least two options and exactly one correct option." });
            if (q.Options.Any(o => string.IsNullOrWhiteSpace(o.Text)))
                return BadRequest(new { message = $"Question {i + 1} has an empty option." });
        }

        var entities = questions.Select(q => new Question
        {
            Id = Guid.NewGuid(),
            DepartmentId = request.DepartmentId,
            CreatedByTeacherId = request.TeacherId,
            Text = q.Text.Trim(),
            Type = q.Type,
            Points = q.Points,
            IsActive = true,
            Options = q.Options.Select(o => new QuestionOption { Id = Guid.NewGuid(), Text = o.Text.Trim(), IsCorrect = o.IsCorrect }).ToList()
        }).ToList();
        db.Questions.AddRange(entities);
        await db.SaveChangesAsync(ct);
        return Ok(new { importedCount = entities.Count, questionIds = entities.Select(q => q.Id), message = "Answer key imported successfully." });
    }

    private static async Task<List<ImportedQuestion>> ParseJsonAsync(Stream stream, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var elements = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToList()
            : new List<JsonElement> { doc.RootElement };
        var result = new List<ImportedQuestion>();
        foreach (var e in elements)
        {
            var text = GetString(e, "text") ?? throw new FormatException("JSON question requires a text field.");
            var type = ParseType(GetString(e, "type") ?? "MultipleChoice");
            var points = GetDecimal(e, "points", 1m);
            var options = new List<ImportedOption>();
            if (e.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
                foreach (var o in opts.EnumerateArray())
                    options.Add(new ImportedOption(GetString(o, "text") ?? "", GetBool(o, "isCorrect")));
            result.Add(new ImportedQuestion(text, type, points, options));
        }
        return result;
    }

    private static string? GetString(JsonElement e, string name)
    {
        foreach (var p in e.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
        return null;
    }
    private static decimal GetDecimal(JsonElement e, string name, decimal fallback)
        => decimal.TryParse(GetString(e, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    private static bool GetBool(JsonElement e, string name)
        => bool.TryParse(GetString(e, name), out var b) && b;

    private static async Task<List<ImportedQuestion>> ParseDelimitedAsync(Stream stream, char delimiter, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var lines = new List<string>();
        while (await reader.ReadLineAsync(ct) is { } line)
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        if (lines.Count < 2) throw new FormatException("Delimited files need a header and at least one data row.");
        var headers = SplitLine(lines[0], delimiter).Select((h, i) => (Name: h.Trim().Trim('"').ToLowerInvariant(), Index: i)).ToDictionary(x => x.Name, x => x.Index);
        if (!headers.ContainsKey("text")) throw new FormatException("Required column: Text.");
        var result = new List<ImportedQuestion>();
        foreach (var line in lines.Skip(1))
        {
            var cells = SplitLine(line, delimiter);
            string Cell(string name) => headers.TryGetValue(name.ToLowerInvariant(), out var index) && index < cells.Count ? cells[index].Trim().Trim('"') : "";
            if (string.IsNullOrWhiteSpace(Cell("text"))) continue;
            var type = ParseType(string.IsNullOrWhiteSpace(Cell("type")) ? "MultipleChoice" : Cell("type"));
            var points = decimal.TryParse(Cell("points"), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 1m;
            result.Add(new ImportedQuestion(Cell("text"), type, points, ParseOptions(Cell("options"))));
        }
        return result;
    }

    private static List<string> SplitLine(string line, char delimiter)
    {
        var result = new List<string>(); var value = new System.Text.StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
            else if (ch == '"') quoted = !quoted;
            else if (ch == delimiter && !quoted) { result.Add(value.ToString()); value.Clear(); }
            else value.Append(ch);
        }
        result.Add(value.ToString()); return result;
    }

    private static async Task<List<ImportedQuestion>> ParseTextAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        if (text.TrimStart().StartsWith("{") || text.TrimStart().StartsWith("["))
        {
            await using var memory = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
            return await ParseJsonAsync(memory, ct);
        }
        await using var lines = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        return await ParseDelimitedAsync(lines, '\t', ct);
    }

    private static async Task<List<ImportedQuestion>> ParseXmlAsync(Stream stream, CancellationToken ct)
    {
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        return doc.Descendants().Where(x => x.Name.LocalName.Equals("Question", StringComparison.OrdinalIgnoreCase)).Select(q =>
        {
            var text = q.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("Text", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
            var typeText = q.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("Type", StringComparison.OrdinalIgnoreCase))?.Value ?? "MultipleChoice";
            var pointsText = q.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("Points", StringComparison.OrdinalIgnoreCase))?.Value;
            var points = decimal.TryParse(pointsText, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 1m;
            var options = q.Descendants().Where(x => x.Name.LocalName.Equals("Option", StringComparison.OrdinalIgnoreCase))
                .Select(o => new ImportedOption(o.Value.Trim(), bool.TryParse(o.Attribute("IsCorrect")?.Value ?? o.Attribute("isCorrect")?.Value, out var correct) && correct)).ToList();
            return new ImportedQuestion(text, ParseType(typeText), points, options);
        }).ToList();
    }

    private static List<ImportedQuestion> ParseExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new FormatException("Workbook has no worksheets.");
        var headerRow = sheet.FirstRowUsed() ?? throw new FormatException("Worksheet is empty.");
        var headers = headerRow.CellsUsed().ToDictionary(c => c.GetString().Trim().ToLowerInvariant(), c => c.Address.ColumnNumber);
        if (!headers.ContainsKey("text")) throw new FormatException("Excel requires a Text column. Optional columns: Type, Points, Options.");
        string Cell(IXLRow row, string name) => headers.TryGetValue(name.ToLowerInvariant(), out var col) ? row.Cell(col).GetString().Trim() : "";
        var result = new List<ImportedQuestion>();
        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var text = Cell(row, "text"); if (string.IsNullOrWhiteSpace(text)) continue;
            var type = ParseType(string.IsNullOrWhiteSpace(Cell(row, "type")) ? "MultipleChoice" : Cell(row, "type"));
            var points = decimal.TryParse(Cell(row, "points"), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 1m;
            result.Add(new ImportedQuestion(text, type, points, ParseOptions(Cell(row, "options"))));
        }
        return result;
    }

    private static List<ImportedQuestion> ParsePdf(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);
        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        var rows = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains('|')).ToList();
        if (rows.Count == 0) throw new FormatException("PDF text must contain one question per line: Text | Type | Points | option A:false;option B:true. Scanned-image PDFs require OCR before import.");
        var result = new List<ImportedQuestion>();
        foreach (var line in rows)
        {
            var cells = line.Split('|', 4).Select(x => x.Trim()).ToArray();
            if (cells.Length < 4 || cells[0].Equals("Text", StringComparison.OrdinalIgnoreCase)) continue;
            var type = ParseType(cells[1]);
            var points = decimal.TryParse(cells[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 1m;
            result.Add(new ImportedQuestion(cells[0], type, points, ParseOptions(cells[3])));
        }
        return result;
    }

    private static List<ImportedOption> ParseOptions(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(item =>
        {
            var parts = item.Split('|', 2);
            if (parts.Length == 1) parts = item.Split(':', 2);
            var correct = parts.Length > 1 && bool.TryParse(parts[1].Trim(), out var b) && b;
            return new ImportedOption(parts[0].Trim(), correct);
        }).ToList();
    }

    private static QuestionType ParseType(string value)
        => value.Trim().Equals("Essay", StringComparison.OrdinalIgnoreCase) || value.Trim() == "1"
            ? QuestionType.Essay : QuestionType.MultipleChoice;
}
