namespace CriaCerto.Api.Configuration;

public static class DotEnvLoader
{
    public static void Load(string? directory = null)
    {
        var current = directory != null ? new DirectoryInfo(directory) : new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current is not null)
        {
            var envFilePath = Path.Combine(current.FullName, ".env");
            if (File.Exists(envFilePath))
            {
                LoadFile(envFilePath);
                return;
            }

            current = current.Parent;
        }

        var baseDirEnv = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
        if (File.Exists(baseDirEnv))
        {
            LoadFile(baseDirEnv);
        }
    }

    public static void LoadFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith("//"))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            if (!string.IsNullOrEmpty(key) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
