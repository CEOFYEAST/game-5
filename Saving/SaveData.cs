using System;
using System.Collections.Generic;
using System.Linq;

namespace game_1.Saving;

// Everything that outlives a launch of the game. Plain data only, kept apart
// from the game objects themselves, so what goes in the file is decided here
// rather than by whatever fields a class happens to have.
public sealed class SaveData
{
    // Raised whenever the shape of a saved run changes, so an older file is
    // recognised and its run dropped instead of loaded wrongly.
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    // How every finished run went, oldest first, across every launch.
    public List<RunResult> Results { get; set; } = [];

    // The run left in progress, or null when there is none to continue.
    public RunSave Run { get; set; }

    // The longest runs, longest first. A tie goes to the run that set the time
    // first, as the sort keeps runs of equal time in the order they were played.
    public List<RunResult> TopResults(int count) =>
        Results.OrderByDescending(result => result.Time).Take(count).ToList();
}

// One finished run: how long it lasted, and when it ended.
public sealed record RunResult(float Time, DateTime FinishedAt);

public sealed record RunSave(
    float RunTime,
    int RemainingTouches,
    float Rotation,
    float TimeSinceLastSpawn,
    List<MobSave> Mobs,
    List<ProjectileSave> Projectiles,
    TilemapSave Floor
);

// Kind names the mob class to rebuild, as the file only holds data.
public sealed record MobSave(string Kind, float X, float Y, int Health);

public sealed record ProjectileSave(
    string Texture,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    int Damage,
    float Scale
);

// Only the seed of the base layer, as the same seed grows the same floor, plus
// the marks the fight has left on it. The size is kept so marks are never laid
// back onto a floor of a different shape.
public sealed record TilemapSave(int Seed, int Columns, int Rows, byte[] Marks);
