using Farm.Core.Chickens;
using System.Text;
using Xunit;

namespace Farm.Core.Tests;

public sealed class ReviewWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Fingerprint_is_stable_across_thread_order()
    {
        var first = Thread(2, "reviewer", Now.AddHours(-3));
        var second = Thread(1, "reviewer", Now.AddHours(-4));

        var forward = FeedbackFingerprint.Create([first, second]);
        var reverse = FeedbackFingerprint.Create([second, first]);

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void Fingerprint_does_not_collide_when_field_delimiters_move()
    {
        var first = new FeedbackThread(1, false, null,
            [new FeedbackComment(2, "a|b", "reviewer", "c", Now)]);
        var second = new FeedbackThread(1, false, null,
            [new FeedbackComment(2, "a", "reviewer", "b|c", Now)]);

        Assert.NotEqual(FeedbackFingerprint.Create([first]), FeedbackFingerprint.Create([second]));
    }

    [Fact]
    public void Eligibility_fingerprint_ignores_authors_own_comments()
    {
        var external = Thread(1, "reviewer", Now.AddHours(-2));
        var withAuthorNoise = external with
        {
            Comments = [.. external.Comments, new FeedbackComment(99, "author", "author", "follow-up", Now.AddHours(-1))]
        };

        var baseline = ReviewEligibilityEvaluator.Evaluate(Candidate([external]), "author", Now, TimeSpan.FromMinutes(30));
        var noisy = ReviewEligibilityEvaluator.Evaluate(Candidate([withAuthorNoise]), "author", Now, TimeSpan.FromMinutes(30));

        Assert.Equal(baseline.Fingerprint, noisy.Fingerprint);
    }

    [Fact]
    public void Eligibility_requires_authorship_external_feedback_and_a_quiet_period()
    {
        var candidate = Candidate([Thread(1, "reviewer", Now.AddHours(-2))]);

        var result = ReviewEligibilityEvaluator.Evaluate(candidate, "author", Now, TimeSpan.FromHours(1));

        Assert.True(result.IsEligible);
        Assert.Equal(ReviewEligibilityReason.Eligible, result.Reason);
    }

    [Fact]
    public void Eligibility_defers_recent_external_feedback()
    {
        var candidate = Candidate([Thread(1, "reviewer", Now.AddMinutes(-30))]);

        var result = ReviewEligibilityEvaluator.Evaluate(candidate, "author", Now, TimeSpan.FromHours(1));

        Assert.False(result.IsEligible);
        Assert.Equal(ReviewEligibilityReason.FeedbackStillCoolingDown, result.Reason);
    }

    [Fact]
    public void Redactor_removes_raw_encoded_authorization_and_url_userinfo_secrets()
    {
        const string secret = "sentinel:p@ssword";
        var redactor = new SensitiveDataRedactor([secret]);
        var text = string.Join('\n',
            secret,
            Uri.EscapeDataString(secret),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(secret)),
            $"Authorization: Bearer {secret}",
            string.Concat("https://user:", secret, "@", "example.test/repo"));

        var result = redactor.Redact(text);

        Assert.DoesNotContain(secret, result);
        Assert.DoesNotContain(Uri.EscapeDataString(secret), result);
        Assert.DoesNotContain(Convert.ToBase64String(Encoding.UTF8.GetBytes(secret)), result);
        Assert.DoesNotContain("user:", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void Sensitive_content_scanner_detects_supported_secret_signatures()
    {
        const string configured = "configured-secret-sentinel";
        var scanner = new SensitiveContentScanner([configured]);
        string[] samples =
        [
            configured,
            "github_" + "pat_" + new string('A', 24),
            new string('A', 75) + "AZDO" + new string('B', 5),
            new string('C', 52),
            "eyJ" + new string('A', 12) + "." + new string('B', 12) + "." + new string('C', 12),
            "-----BEGIN " + "PRIVATE KEY-----",
            string.Concat("Server=db;", "Password", "=", "secret-value"),
            "api_key = \"" + "secret-value" + "\"",
            "https://user:" + "secret-value" + "@example.test/repo"
        ];

        Assert.All(samples, sample => Assert.NotEmpty(scanner.Find(sample)));
    }

    [Fact]
    public void Sensitive_content_scanner_accepts_ordinary_code()
    {
        var scanner = new SensitiveContentScanner([]);

        var findings = scanner.Find("var token = tokenProvider.GetToken();\nconnection.Open();\nAssert.Equal(expected, actual);");

        Assert.Empty(findings);
    }

    private static ReviewCandidate Candidate(IReadOnlyList<FeedbackThread> threads) => new(
        "org", "project", "repo-id", "repo", new Uri("https://example/repo"), 42, "PR", "refs/heads/feature",
        "refs/heads/main", "abc", "author", threads, []);

    private static FeedbackThread Thread(int id, string authorId, DateTimeOffset publishedAt) => new(
        id, false, new FeedbackAnchor("file.cs", 10, 10, "abc"),
        [new FeedbackComment(id, authorId, authorId, "feedback", publishedAt)]);
}