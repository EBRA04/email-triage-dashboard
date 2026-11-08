using EmailTriage.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailTriage.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestionController : ControllerBase
{
    private readonly GmailIngestionService _gmail;

    public IngestionController(GmailIngestionService gmail)
    {
        _gmail = gmail;
    }

    /// Manual trigger: pulls recent inbox messages into RawEmails.
    /// First call will open a browser for Google consent (one-time).
    [HttpPost("gmail/run")]
    public async Task<IActionResult> RunGmailIngestion([FromQuery] int max = 10, CancellationToken ct = default)
    {
        var inserted = await _gmail.IngestRecentAsync(max, ct);
        return Ok(new { inserted });
    }
}
