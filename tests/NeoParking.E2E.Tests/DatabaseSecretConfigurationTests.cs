namespace NeoParking.E2E.Tests;

using FluentAssertions;
using MySqlConnector;
using NeoParking.Shared.Kernel.Configuration;

public class DatabaseSecretConfigurationTests
{
    private const string WithoutPassword = "Server=mysql;Database=neoparking_access;Uid=neoparking_app;";

    [Fact]
    public void WithPassword_ShouldApplyThePasswordAndKeepTheOtherOptions()
    {
        var result = new MySqlConnectionStringBuilder(MySqlConnectionStrings.WithPassword(WithoutPassword, "a;b=c"));

        result.Password.Should().Be("a;b=c");
        result.Server.Should().Be("mysql");
        result.UserID.Should().Be("neoparking_app");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithPassword_WithoutPassword_ShouldNameTheVariable(string? password)
    {
        var act = () => MySqlConnectionStrings.WithPassword(WithoutPassword, password);

        act.Should().Throw<InvalidOperationException>().WithMessage("*MYSQL_PASSWORD*");
    }

    [Fact]
    public void LocalEnvFile_ShouldFindTheFileAboveAndKeepExistingVariables()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (added, existing, hash, comment) =
            ($"ADDED_{suffix}", $"EXISTING_{suffix}", $"HASH_{suffix}", $"COMMENT_{suffix}");
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), suffix));
        var nested = root.CreateSubdirectory("src/api");
        File.WriteAllLines(Path.Combine(root.FullName, ".env"),
        [
            "# comment",
            $"{added}=from-file",
            $"{existing}=from-file",
            $"{hash}=a#b",
            $"{comment}=value # note",
        ]);
        Environment.SetEnvironmentVariable(existing, "from-environment");

        try
        {
            LocalEnvFile.Load(nested.FullName);

            Environment.GetEnvironmentVariable(added).Should().Be("from-file");
            Environment.GetEnvironmentVariable(existing).Should().Be("from-environment");
            Environment.GetEnvironmentVariable(hash).Should().Be("a#b");
            Environment.GetEnvironmentVariable(comment).Should().Be("value");
        }
        finally
        {
            foreach (var key in new[] { added, existing, hash, comment })
                Environment.SetEnvironmentVariable(key, null);
            root.Delete(recursive: true);
        }
    }
}
