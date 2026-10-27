using System.Globalization;
using System.Text.Json;

namespace GTARadioEditor;

public static class L
{
    public sealed record Language(string Code, string Name)
    {
        public override string ToString() => Name;
    }
    public static IReadOnlyList<Language> Languages { get; } = new Language[]
    {
        new("en", "English"), new("es", "Español"), new("ru", "Русский"),
        new("zh-CN", "简体中文"), new("pt", "Português"), new("de", "Deutsch")
    };
    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = LoadCatalogs();
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GTARadioEditor", "settings.json");
    private static string _code = "en";
    public static string Code => _code;
    public static event Action? Changed;

    private static Dictionary<string, Dictionary<string, string>> LoadCatalogs()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in Languages)
        {
            using var stream = typeof(L).Assembly.GetManifestResourceStream($"GTARadioEditor.Locales.{language.Code}.json")
                ?? throw new InvalidDataException($"Missing embedded language: {language.Code}");
            result.Add(language.Code, JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidDataException($"Invalid language catalog: {language.Code}"));
        }
        return result;
    }

    public static string T(string key, params object?[] arguments)
    {
        var current = Catalogs[_code];
        var text = current.TryGetValue(key, out var translated) ? translated :
            Catalogs["en"].TryGetValue(key, out var english) ? english : key;
        return arguments.Length == 0 ? text : string.Format(CultureInfo.GetCultureInfo(_code), text, arguments);
    }

    internal static string? KeyForText(string text)
    {
        if (Catalogs["en"].ContainsKey(text)) return text;
        return Catalogs[_code].FirstOrDefault(pair => pair.Value == text).Key;
    }

    public static void SetLanguage(string code, bool persist = true)
    {
        if (!Catalogs.ContainsKey(code)) throw new ArgumentException("Unsupported language.", nameof(code));
        _code = code;
        if (persist)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Dictionary<string, string> { ["language"] = code }));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        Changed?.Invoke();
    }

    public static void LoadPreference()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsPath));
                if (settings is not null && settings.TryGetValue("language", out var code) && Catalogs.ContainsKey(code))
                    SetLanguage(code, persist: false);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
    }
}
