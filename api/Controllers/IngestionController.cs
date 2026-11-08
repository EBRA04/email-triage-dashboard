using EmailTriage.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailTriage.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestionController : ControllerBase
{
    private readonly GmailIngestionService _gmail;
    private readonly ClassificationService _classifier;

    public IngestionController(GmailIngestionService gmail, ClassificationService classifier)
    {
        _gmail = gmail;
        _classifier = classifier;
    }

    /// Manual trigger: pulls recent inbox messages into RawEmails.
    /// First call will open a browser for Google consent (one-time).
    [HttpPost("gmail/run")]
    public async Task<IActionResult> RunGmailIngestion([FromQuery] int max = 10, CancellationToken ct = default)
    {
        var inserted = await _gmail.IngestRecentAsync(max, ct);
        return Ok(new { inserted });
    }

    /// Classifies every RawEmail that doesn't have a Ticket yet.
    [HttpPost("classify/run")]
    public async Task<IActionResult> RunClassification(CancellationToken ct = default)
    {
        var created = await _classifier.ClassifyUnprocessedAsync(ct: ct);
        return Ok(new { created });
    }

    /// Convenience: ingest then classify in one call.
    [HttpPost("run")]
    public async Task<IActionResult> RunFull([FromQuery] int max = 10, CancellationToken ct = default)
    {
        var inserted = await _gmail.IngestRecentAsync(max, ct);
        var created = await _classifier.ClassifyUnprocessedAsync(ct: ct);
        return Ok(new { inserted, created });
    }
}
