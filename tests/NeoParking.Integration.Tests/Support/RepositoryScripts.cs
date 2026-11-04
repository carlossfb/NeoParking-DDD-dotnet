namespace NeoParking.Integration.Tests.Support;

/// <summary>Lê os scripts operacionais de scripts/mysql, apontando para os schemas do teste.</summary>
public static class RepositoryScripts
{
    public static async Task<string> ReadAsync(string name, TestDatabases databases)
    {
        var script = await File.ReadAllTextAsync(Find(name));

        return script
            .Replace("neoparking_access", databases.Access)
            .Replace("neoparking_management", databases.Management)
            .Replace("neoparking_billing", databases.Billing);
    }

    private static string Find(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var script = Path.Combine(dir.FullName, "scripts", "mysql", name);
            if (File.Exists(script))
                return script;
        }

        throw new FileNotFoundException($"scripts/mysql/{name} not found above the test output directory.");
    }
}
