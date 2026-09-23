using Farm.Core.Chickens;
using Xunit;

namespace Farm.Sandbox.Chickens.Tests;

public sealed class ChickenRunnerOutcomeTests
{
    [Fact]
    public void Failed_brain_outcome_is_preserved_even_without_a_patch()
    {
        var failed = new ReviewOutcome(ReviewOutcomeKind.Failed, "empty response");

        var outcome = ChickenRunner.DetermineOutcome(failed, string.Empty, new ValidationResult(false, -1, "not run"));

        Assert.Equal(failed, outcome);
    }

    [Fact]
    public void Changes_require_successful_configured_validation()
    {
        var explanation = new ReviewOutcome(ReviewOutcomeKind.ExplanationOnly, "done");

        var outcome = ChickenRunner.DetermineOutcome(explanation, "patch", new ValidationResult(false, -1, "No validation commands are configured."));

        Assert.Equal(ReviewOutcomeKind.Failed, outcome.Kind);
    }
}