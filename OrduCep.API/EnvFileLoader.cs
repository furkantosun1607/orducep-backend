namespace OrduCep.API;

public static class EnvFileLoader
{
    public static void Load()
    {
        foreach (var path in FindEnvFiles())
        {
            LoadFile(path);
        }
    }

    private static IEnumerable<string> FindEnvFiles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(root);

            while (current != null)
            {
                foreach (var candidate in new[]
                {
                    Path.Combine(current.FullName, ".env"),
                    Path.Combine(current.FullName, "OrduCep.API", ".env")
                })
                {
                    if (File.Exists(candidate) && seen.Add(candidate))
                        yield return candidate;
                }

                current = current.Parent;
            }
        }
    }

    private static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                line = line["export ".Length..].TrimStart();

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (key.Length == 0 || Environment.GetEnvironmentVariable(key) != null)
                continue;

            Environment.SetEnvironmentVariable(key, Unquote(value));
        }
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2)
            return value;

        var first = value[0];
        var last = value[^1];

        if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            return value[1..^1];

        return value;
    }
}
