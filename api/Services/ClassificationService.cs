using EmailTriage.Api.Data;
using EmailTriage.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EmailTriage.Api.Services;

public record ClassificationResult(
    string DepartmentSlug,
    double Confidence,
    string Summary,
    string Reasoning
);

public class ClassificationService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ClassificationService> _logger;

    private const string Model = "openai/gpt-oss-120b";

    public ClassificationService(IConfiguration config, AppDbContext db, IHttpClientFactory httpFactory, ILogger<ClassificationService> logger)
    {
        _config = config;
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(RawEmail email, List<Department> departments, CancellationToken ct = default)
    {
        var apiKey = _config["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Groq:ApiKey is not configured.");

        var deptList = string.Join(", ", departments.Select(d => $"\"{d.Slug}\" ({d.Name})"));

        var systemPrompt =
            $"You are an email triage classifier for a company's contact-us inbox. " +
            $"Given an email, decide which department should handle it. " +
            $"Valid department slugs: {deptList}, or \"other\" if none fit. " +
            "Respond with ONLY a JSON object, no markdown, in this exact shape: " +
            "{\"department\":\"<slug>\",\"confidence\":<0.0-1.0>,\"summary\":\"<one or two sentence summary of the request>\",\"reasoning\":\"<brief reason for the department choice>\"}";

        var userPrompt =
            $"From: {email.From}\nSubject: {email.Subject}\n\nBody:\n{Truncate(email.BodyText, 4000)}";

        var requestBody = new
        {
            model = Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.groq.com/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("openai/v1/chat/completions", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Groq API error {Status}: {Body}", response.StatusCode, errorBody);
            throw new HttpRequestException($"Groq API returned {response.StatusCode}: {errorBody}");
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(raw);
        var messageContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        using var parsed = JsonDocument.Parse(messageContent);
        var root = parsed.RootElement;

        var deptSlug = root.TryGetProperty("department", out var d) ? d.GetString() ?? "other" : "other";
        var confidence = root.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0;
        var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? string.Empty : string.Empty;
        var reasoning = root.TryGetProperty("reasoning", out var r) ? r.GetString() ?? string.Empty : string.Empty;

        return new ClassificationResult(deptSlug, confidence, summary, reasoning);
    }

    /// Classifies every RawEmail that doesn't have a Ticket yet, creating one
    /// for each. Low-confidence results go to NeedsReview instead of being
    /// auto-routed, per the triage policy.
    public async Task<int> ClassifyUnprocessedAsync(double confidenceThreshold = 0.6, CancellationToken ct = default)
    {
        var departments = await _db.Departments.ToListAsync(ct);

        var unprocessed = await _db.RawEmails
            .Where(r => r.Ticket == null)
            .ToListAsync(ct);

        var created = 0;

        foreach (var email in unprocessed)
        {
            ClassificationResult result;
            try
            {
                result = await ClassifyAsync(email, departments, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Classification failed for RawEmail {Id}", email.Id);
                continue;
            }

            var matchedDept = departments.FirstOrDefault(d =>
                string.Equals(d.Slug, result.DepartmentSlug, StringComparison.OrdinalIgnoreCase));

            var needsReview = matchedDept == null || result.Confidence < confidenceThreshold;

            var ticket = new Ticket
            {
                RawEmailId = email.Id,
                DepartmentId = matchedDept?.Id,
                Status = needsReview ? TicketStatus.NeedsReview : TicketStatus.New,
                AiDepartmentGuess = result.DepartmentSlug,
                AiConfidence = result.Confidence,
                AiSummary = result.Summary,
                AiReasoning = result.Reasoning,
                ClassifiedAt = DateTime.UtcNow
            };

            _db.Tickets.Add(ticket);
            await _db.SaveChangesAsync(ct); // save now to get ticket.Id for audit log

            _db.AuditLogs.Add(new AuditLog
            {
                TicketId = ticket.Id,
                ActorId = null,
                Action = AuditAction.Classified,
                Detail = $"AI classified as '{result.DepartmentSlug}' (confidence {result.Confidence:P0}). {result.Reasoning}"
            });

            created++;
        }

        if (created > 0)
            await _db.SaveChangesAsync(ct);

        return created;
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "... [truncated]";
}
