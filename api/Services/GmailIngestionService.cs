using EmailTriage.Api.Data;
using EmailTriage.Api.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace EmailTriage.Api.Services;

public class GmailIngestionService
{
    private static readonly string[] Scopes = { GmailService.Scope.GmailReadonly };
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogger<GmailIngestionService> _logger;

    public GmailIngestionService(IConfiguration config, AppDbContext db, ILogger<GmailIngestionService> logger)
    {
        _config = config;
        _db = db;
        _logger = logger;
    }

    private async Task<GmailService> GetGmailServiceAsync(CancellationToken ct)
    {
        var clientId = _config["Gmail:ClientId"];
        var clientSecret = _config["Gmail:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Gmail:ClientId / Gmail:ClientSecret are not configured.");

        // Token is cached under api/.gmail-credentials so subsequent runs
        // don't need a new browser consent (silent refresh via stored token).
        var credPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".gmail-credentials");

        var secrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "user",
            ct,
            new FileDataStore(credPath, true));

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "EmailTriage"
        });
    }

    /// Pulls the most recent N messages from INBOX and stores any not
    /// already ingested as immutable RawEmail rows.
    public async Task<int> IngestRecentAsync(int maxResults, CancellationToken ct = default)
    {
        var gmail = await GetGmailServiceAsync(ct);

        var listRequest = gmail.Users.Messages.List("me");
        listRequest.LabelIds = "INBOX";
        listRequest.MaxResults = maxResults;
        var listResponse = await listRequest.ExecuteAsync(ct);

        var inserted = 0;

        if (listResponse.Messages == null) return inserted;

        foreach (var msgRef in listResponse.Messages)
        {
            if (await _db.RawEmails.AnyAsync(r => r.GraphMessageId == msgRef.Id, ct))
                continue; // already ingested

            var getRequest = gmail.Users.Messages.Get("me", msgRef.Id);
            getRequest.Format = Google.Apis.Gmail.v1.UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
            var full = await getRequest.ExecuteAsync(ct);

            var headers = full.Payload?.Headers ?? new List<MessagePartHeader>();
            string Header(string name) => headers.FirstOrDefault(h =>
                string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

            var (bodyText, bodyHtml) = ExtractBody(full.Payload);

            var raw = new RawEmail
            {
                GraphMessageId = full.Id,
                InternetMessageId = Header("Message-Id"),
                From = Header("From"),
                FromName = null,
                To = Header("To"),
                Subject = Header("Subject"),
                BodyText = bodyText,
                BodyHtml = bodyHtml,
                ReceivedAt = full.InternalDate.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(full.InternalDate.Value).UtcDateTime
                    : DateTime.UtcNow,
                RawJson = JsonSerializer.Serialize(full)
            };

            _db.RawEmails.Add(raw);
            inserted++;
        }

        if (inserted > 0)
            await _db.SaveChangesAsync(ct);

        return inserted;
    }

    private static (string text, string? html) ExtractBody(MessagePart? part)
    {
        if (part == null) return (string.Empty, null);

        string? text = null, html = null;

        void Walk(MessagePart p)
        {
            if (p.MimeType == "text/plain" && p.Body?.Data != null)
                text ??= DecodeBase64Url(p.Body.Data);
            else if (p.MimeType == "text/html" && p.Body?.Data != null)
                html ??= DecodeBase64Url(p.Body.Data);

            if (p.Parts != null)
                foreach (var sub in p.Parts) Walk(sub);
        }

        Walk(part);

        return (text ?? string.Empty, html);
    }

    private static string DecodeBase64Url(string data)
    {
        var padded = data.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        var bytes = Convert.FromBase64String(padded);
        return Encoding.UTF8.GetString(bytes);
    }
}
