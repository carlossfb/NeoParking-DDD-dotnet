namespace NeoParking.Shared.Kernel.Configuration;

/// <summary>
/// Carrega o .env local para quem roda fora do compose (dotnet run em Development e dotnet ef).
/// Sobe a árvore de diretórios até encontrá-lo e nunca sobrescreve uma variável já definida.
/// </summary>
public static class LocalEnvFile
{
    public static void Load(string? startDirectory = null)
    {
        for (var directory = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, ".env");
            if (!File.Exists(path))
                continue;

            foreach (var line in File.ReadAllLines(path))
                Apply(line);

            return;
        }
    }

    private static void Apply(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            return;

        var parts = line.Split('=', 2);
        if (parts.Length != 2)
            return;

        var key = parts[0].Trim();
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, ParseValue(parts[1]));
    }

    // Como no compose: "#" só inicia comentário depois de um espaço, para não cortar um valor.
    private static string ParseValue(string raw)
    {
        var value = raw.Trim();
        if (value.Length >= 2 && (value[0] is '"' or '\'') && value[^1] == value[0])
            return value[1..^1];

        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        return comment >= 0 ? value[..comment].TrimEnd() : value;
    }
}
