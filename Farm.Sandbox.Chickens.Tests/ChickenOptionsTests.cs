using Xunit;

namespace Farm.Sandbox.Chickens.Tests;

public sealed class ChickenOptionsTests
{
    [Fact]
    public void Safe_process_environment_loads_explicit_values()
    {
        var previous = Environment.GetEnvironmentVariable("FARM_CHICKENS_SAFE_PROCESS_ENV_JSON");
        try
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_SAFE_PROCESS_ENV_JSON", "{\"CI\":\"true\",\"FEATURE_FLAG\":\"enabled\"}");

            var environment = ChickenOptions.LoadSafeProcessEnvironment();

            Assert.Equal("true", environment["CI"]);
            Assert.Equal("enabled", environment["FEATURE_FLAG"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_SAFE_PROCESS_ENV_JSON", previous);
        }
    }

    [Fact]
    public void Push_defaults_disabled_and_requires_explicit_true_to_enable()
    {
        var artifacts = Environment.GetEnvironmentVariable("FARM_CHICKENS_ARTIFACTS_PATH");
        var enablePush = Environment.GetEnvironmentVariable("FARM_CHICKENS_ENABLE_PUSH");
        try
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ARTIFACTS_PATH", Path.GetTempPath());
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ENABLE_PUSH", null);
            Assert.False(ChickenOptions.FromEnvironment().EnablePush);

            Environment.SetEnvironmentVariable("FARM_CHICKENS_ENABLE_PUSH", "true");
            Assert.True(ChickenOptions.FromEnvironment().EnablePush);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ARTIFACTS_PATH", artifacts);
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ENABLE_PUSH", enablePush);
        }
    }

    [Fact]
    public void Status_path_must_remain_under_state_root()
    {
        var options = new ChickenOptions { ArtifactsPath = Path.GetTempPath(), StateRootPath = Path.Combine(Path.GetTempPath(), "state") };

        options.ValidatePathUnderStateRoot(Path.Combine(options.StateRootPath, "status.json"));

        Assert.Throws<InvalidOperationException>(() => options.ValidatePathUnderStateRoot(Path.Combine(Path.GetTempPath(), "status.json")));
    }

    [Fact]
    public void State_paths_default_under_configured_root()
    {
        var artifacts = Environment.GetEnvironmentVariable("FARM_CHICKENS_ARTIFACTS_PATH");
        var root = Environment.GetEnvironmentVariable("FARM_CHICKENS_STATE_CONTAINER_PATH");
        var state = Environment.GetEnvironmentVariable("FARM_CHICKENS_STATE_PATH");
        var status = Environment.GetEnvironmentVariable("FARM_CHICKENS_STATUS_PATH");
        var lockPath = Environment.GetEnvironmentVariable("FARM_CHICKENS_LOCK_PATH");
        var configuredRoot = Path.Combine(Path.GetTempPath(), "chicken-state-defaults");
        try
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ARTIFACTS_PATH", Path.GetTempPath());
            Environment.SetEnvironmentVariable("FARM_CHICKENS_STATE_CONTAINER_PATH", configuredRoot);
            Environment.SetEnvironmentVariable("FARM_CHICKENS_STATE_PATH", null);
            Environment.SetEnvironmentVariable("FARM_CHICKENS_STATUS_PATH", null);
            Environment.SetEnvironmentVariable("FARM_CHICKENS_LOCK_PATH", null);

            var options = ChickenOptions.FromEnvironment();

            Assert.Equal(Path.Combine(configuredRoot, "chickens.db"), options.StatePath);
            Assert.Equal(Path.Combine(configuredRoot, "status.json"), options.StatusPath);
            Assert.Equal(Path.Combine(configuredRoot, "chickens.lock"), options.LockPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ARTIFACTS_PATH", artifacts);
            Environment.SetEnvironmentVariable("FARM_CHICKENS_STATE_CONTAINER_PATH", root);
            Environment.SetEnvironmentVariable("FARM_CHICKENS_STATE_PATH", state);
            Environment.SetEnvironmentVariable("FARM_CHICKENS_STATUS_PATH", status);
            Environment.SetEnvironmentVariable("FARM_CHICKENS_LOCK_PATH", lockPath);
        }
    }

    [Fact]
    public void Nested_custom_state_paths_are_allowed()
    {
        var root = Path.Combine(Path.GetTempPath(), "chicken-state-nested");
        var options = new ChickenOptions
        {
            ArtifactsPath = Path.GetTempPath(),
            StateRootPath = root,
            StatePath = Path.Combine(root, "nested", "chickens.db"),
            StatusPath = Path.Combine(root, "nested", "status.json"),
            LockPath = "nested/chickens.lock"
        };

        options.ValidateStatePaths();
    }

    [Theory]
    [InlineData("state")]
    [InlineData("status")]
    [InlineData("lock")]
    public void Absolute_and_parent_traversal_paths_are_rejected(string pathKind)
    {
        var root = Path.Combine(Path.GetTempPath(), "chicken-state-confined");
        var escapingPath = Path.Combine(root, "..", "outside", pathKind + ".path");
        var options = new ChickenOptions
        {
            ArtifactsPath = Path.GetTempPath(),
            StateRootPath = root,
            StatePath = pathKind == "state" ? escapingPath : Path.Combine(root, "chickens.db"),
            StatusPath = pathKind == "status" ? escapingPath : Path.Combine(root, "status.json"),
            LockPath = pathKind == "lock" ? escapingPath : Path.Combine(root, "chickens.lock")
        };

        Assert.Throws<InvalidOperationException>(options.ValidateStatePaths);
        Assert.Throws<InvalidOperationException>(() => options.ValidatePathUnderStateRoot(Path.Combine(root, "..", "outside.path")));
    }
}