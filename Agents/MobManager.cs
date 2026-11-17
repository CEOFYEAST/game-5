using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using game_1.Saving;

namespace game_1.Agents;

public class MobManager
{
    // A run opens at this many seconds between spawns.
    private const float StartSpawnInterval = 1.2f;

    // ...and tightens to this, which is as fast as spawns ever come.
    private const float MinSpawnInterval = 0.45f;

    // Seconds of survival it takes to go from one to the other.
    private const float SpawnRampDuration = 120f;

    // Seconds of survival that buy one more mob on every spawn.
    private const float ExtraMobInterval = 75f;

    // However long the run lasts, a single spawn never exceeds this.
    private const int MaxMobsPerSpawn = 3;

    // Ticks move at twice a zombie's pace, so they are held back until the
    // player has had time to settle in. Until then every spawn is a zombie.
    private const float TickDelay = 20f;

    // The names mobs are saved under. Written out rather than taken from the
    // class names, so renaming a class does not break every save.
    private const string ZombieKind = "Zombie";
    private const string TickKind = "Tick";

    private readonly List<Mob> _mobs = [];

    private readonly Random _random = new();

    private readonly GraphicsDevice _graphicsDevice;

    private ContentManager _content;

    private float _timeSinceLastSpawn = StartSpawnInterval;

    public IReadOnlyList<Mob> Mobs => _mobs;

    // How far along the wait for the next spawn is, so a save picks up mid-wait.
    public float TimeSinceLastSpawn => _timeSinceLastSpawn;

    public MobManager(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public void LoadContent(ContentManager content)
    {
        _content = content;
    }

    // Clears the field so a new run starts with no mobs left over from the last.
    public void Reset()
    {
        _mobs.Clear();

        // Left at a full interval so the opening spawn lands straight away.
        _timeSinceLastSpawn = StartSpawnInterval;
    }

    // runTime is how long the player has survived; everything about how hard
    // the run is pushing back is read off it, so difficulty cannot drift out of
    // step with the time on screen.
    public void Update(GameTime gameTime, Vector2 playerPosition, float runTime)
    {
        _timeSinceLastSpawn += (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Held still for the whole frame, so shrinking it can never outrun the
        // loop below and spawn forever.
        float interval = SpawnIntervalAt(runTime);

        // A loop rather than an if, so a long frame still owes the right number of spawns.
        while (_timeSinceLastSpawn >= interval)
        {
            _timeSinceLastSpawn -= interval;

            int count = MobsPerSpawnAt(runTime);
            for (int i = 0; i < count; i++)
                Spawn(runTime);
        }

        foreach (Mob mob in _mobs)
            mob.Update(gameTime, playerPosition);

        _mobs.RemoveAll(mob => !mob.IsAlive);
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (Mob mob in _mobs)
            mob.Draw(gameTime, spriteBatch);
    }

    // Spawns tighten steadily over the first SpawnRampDuration seconds, then
    // hold, so the run keeps its hardest footing rather than running away.
    private static float SpawnIntervalAt(float runTime) =>
        MathHelper.Lerp(
            StartSpawnInterval,
            MinSpawnInterval,
            MathHelper.Clamp(runTime / SpawnRampDuration, 0f, 1f)
        );

    // One more mob per spawn for every ExtraMobInterval survived. The part of
    // a mob left over is paid as a chance at one extra, so the pressure climbs
    // smoothly instead of doubling the moment a threshold is crossed.
    private int MobsPerSpawnAt(float runTime)
    {
        float count = MathF.Min(1f + runTime / ExtraMobInterval, MaxMobsPerSpawn);
        int whole = (int)count;

        return _random.NextDouble() < count - whole ? whole + 1 : whole;
    }

    public List<MobSave> Capture()
    {
        List<MobSave> saves = [];

        foreach (Mob mob in _mobs)
        {
            if (mob.IsAlive)
                saves.Add(new MobSave(KindOf(mob), mob.Position.X, mob.Position.Y, mob.Health));
        }

        return saves;
    }

    // Replaces the field with the mobs from a save. A kind this build does not
    // know is skipped rather than guessed at.
    public void Restore(IEnumerable<MobSave> saves, float timeSinceLastSpawn)
    {
        Reset();
        _timeSinceLastSpawn = timeSinceLastSpawn;

        if (saves is null) return;

        foreach (MobSave save in saves)
        {
            Mob mob = Create(save.Kind);
            if (mob is null) continue;

            mob.Position = new Vector2(save.X, save.Y);
            mob.RestoreHealth(save.Health);
            _mobs.Add(mob);
        }
    }

    private void Spawn(float runTime)
    {
        // An even split once ticks are in play, and nothing but zombies before.
        bool spawnTick = runTime >= TickDelay && _random.Next(2) == 0;

        Mob mob = Create(spawnTick ? TickKind : ZombieKind);

        // The texture has to be loaded before we know how far off-screen to place it.
        mob.Position = PointJustOffScreen(mob.DrawRadius);

        _mobs.Add(mob);
    }

    // Spawning and loading both build mobs here, so a new kind of mob only
    // needs adding in one place to be both spawned and saved.
    private Mob Create(string kind)
    {
        Mob mob = kind switch
        {
            TickKind => new Tick(Vector2.Zero),
            ZombieKind => new Zombie(Vector2.Zero),
            _ => null
        };

        mob?.LoadContent(_content);
        return mob;
    }

    private static string KindOf(Mob mob) => mob switch
    {
        Tick => TickKind,
        Zombie => ZombieKind,
        _ => throw new ArgumentException($"No save name for {mob.GetType().Name}.", nameof(mob))
    };

    // Picks a point on the viewport rectangle grown by margin, uniformly by
    // distance along that perimeter. Choosing an edge first and then a point on
    // it would crowd the short edges, so instead one distance is drawn across
    // the whole loop and then walked around the corners.
    private Vector2 PointJustOffScreen(float margin)
    {
        Rectangle bounds = _graphicsDevice.Viewport.Bounds;

        float left = bounds.Left - margin;
        float top = bounds.Top - margin;
        float width = bounds.Width + margin * 2f;
        float height = bounds.Height + margin * 2f;

        float distance = (float)_random.NextDouble() * (width + height) * 2f;

        if (distance < width)
            return new Vector2(left + distance, top);

        distance -= width;
        if (distance < height)
            return new Vector2(left + width, top + distance);

        distance -= height;
        if (distance < width)
            return new Vector2(left + width - distance, top + height);

        distance -= width;
        return new Vector2(left, top + height - distance);
    }
}
