using System;
using System.IO;
using System.Text.Json;

namespace game_1.Saving;

// Reads and writes the one save file. A missing, damaged or out of date file
// is never an error worth stopping for: the game just starts as if fresh.
public static class SaveFile
{
    // Indented so the file can be opened and read, which costs nothing at
    // this size.
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TopDownShooter",
        "save.json"
    );

    public static SaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new SaveData();

            SaveData data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath), Options);
            if (data is null) return new SaveData();

            // A file written by hand, or before results were kept, may have none.
            data.Results ??= [];
            data.Results.RemoveAll(result => result is null);

            // A run saved in an older shape cannot be trusted to load, but a
            // finished run's time means the same thing in every version.
            if (data.Version != SaveData.CurrentVersion)
                return new SaveData { Results = data.Results };

            return data;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new SaveData();
        }
    }

    // Returns false if the file could not be written.
    public static bool Write(SaveData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            // Written beside the real file and then swapped in, so a crash
            // part way through never leaves half a save behind.
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, Options));
            File.Move(temporary, FilePath, overwrite: true);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
