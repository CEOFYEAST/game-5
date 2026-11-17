using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using game_1.Agents;
using game_1.Audio;
using game_1.Particles;
using game_1.Projectiles;
using game_1.Saving;
using game_1.Weapons;
using game_1.World;

namespace game_1;

public class TopDownShooter : Game
{
    // The title screen, in the order it reads: what the player is trying to
    // do and the things they can do about it. How to begin follows, and
    // depends on whether there is a run saved to continue.
    private static readonly string[] StartMessages =
    [
        "Survive as long as you can",
        "Space - shoot",
        "Q - melee",
        "Esc - pause"
    ];

    private const string StartMessage = "Press Enter to start";

    private const string NewRunMessage = "Enter - new run";

    private const string PausedMessage = "Paused";

    private const string ResumeMessage = "Esc - resume";

    private const string SaveAndQuitMessage = "S - save and quit";

    private const string SaveFailedMessage = "Could not save - S to try again";

    // How dark the world goes behind the pause prompt, so the text reads
    // over a crowded field.
    private const float PauseDim = 0.55f;

    private const string GameOverMessage = "Game over";

    private const string RestartMessage = "Enter to play again, Esc to exit";

    private const string LeaderboardTitle = "Best times";

    // How many of the longest runs the game over screen ranks.
    private const int LeaderboardSize = 3;

    // Picks out the run just played when it makes the leaderboard.
    private static readonly Color HighlightColor = new(255, 200, 80);

    // Vertical space between a prompt and the player sprite under it.
    private const float PromptGap = 24f;

    // Vertical space between two lines of the same prompt.
    private const float LineGap = 6f;

    // Space between the top of the screen and the run timer under it.
    private const float TimerMargin = 16f;

    // Space between the top left corner and the row of hearts in it.
    private const float HeartMargin = 16f;

    // Space between one heart and the next.
    private const float HeartGap = 6f;

    // How tall each heart is drawn, in pixels.
    private const float HeartHeight = 32f;

    // What is left of a heart once it has been spent. Lost hearts stay in
    // place at this strength, so the row shows how much health there was as
    // well as how much is left.
    private const float LostHeartFade = 0.2f;

    // Dark, as particles are drawn additively: they can only brighten what is
    // behind them, and on anything lighter they wash out instead of glowing.
    private static readonly Color BackgroundColor = new(22, 24, 36);

    // How hard each event jolts the screen, in pixels.
    private const float ShotShake = 1.5f;
    private const float EnemyDeathShake = 4f;
    private const float PlayerDamageShake = 14f;

    // Muzzle flash: a tight fan of sparks along the shot.
    private static readonly Color MuzzleColor = new(255, 200, 80);
    private const float MuzzleSpread = 0.7f;
    private const int MuzzleCount = 10;
    private const float MuzzleSpeed = 260f;
    private const float MuzzleSize = 9f;

    // What any hit on a mob throws off, whether or not it survives.
    private static readonly Color EnemyHitColor = new(255, 245, 210);
    private const int EnemyHitCount = 10;
    private const float EnemyHitSpeed = 180f;
    private const float EnemyHitSize = 7f;

    // The larger burst on top of that when the mob goes down.
    private static readonly Color EnemyDeathColor = new(255, 110, 30);
    private const int EnemyDeathCount = 36;
    private const float EnemyDeathSpeed = 240f;
    private const float EnemyDeathSize = 12f;

    // The biggest of the lot, as losing a heart is the thing not to miss.
    private static readonly Color PlayerDamageColor = new(255, 40, 60);
    private const int PlayerDamageCount = 70;
    private const float PlayerDamageSpeed = 340f;
    private const float PlayerDamageSize = 14f;

    private GraphicsDeviceManager _graphics;

    private SpriteBatch _spriteBatch;

    private SpriteFont _font;

    private Texture2D _heartTexture;

    private SoundBank _sounds;

    private Player _player;

    private MobManager _mobManager;

    private ProjectileManager _projectileManager;

    private ParticleManager _particles;

    private readonly ScreenShake _screenShake = new();

    private Tilemap _tilemap;

    // One white pixel, stretched over the screen to dim it while paused.
    private Texture2D _pixel;

    // The best time and any run left in progress, read once at launch and
    // written back whenever either changes.
    private SaveData _save;

    private GameState _state = GameState.Start;

    // The prompt on screen, a line at a time, each with the position it was
    // laid out at. Empty while a run is in progress.
    private readonly List<(string Text, Vector2 Position, Color Color)> _promptLines = [];

    // How long the current run has lasted, in seconds.
    private float _runTime;

    // Kept so firing and swinging trigger on the press, not every frame held.
    private KeyboardState _previousKeyboard;

    public TopDownShooter()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _player = new(
            GraphicsDevice.Viewport.Bounds.Center.ToVector2()
        );
        _mobManager = new(GraphicsDevice);
        _projectileManager = new();
        _particles = new(GraphicsDevice);
        _sounds = new();
        _tilemap = new(GraphicsDevice, GraphicsDevice.Viewport.Bounds);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        // A saved run's floor goes down straight away, so the title screen
        // already shows the ground that was being held.
        _save = SaveFile.Load();
        _tilemap.Restore(_save.Run?.Floor);

        // Anything already held as the game opens - the Enter that launched it
        // from a terminal, say - counts as held, not as a fresh press, so the
        // title screen is not skipped before it has been read.
        _previousKeyboard = Keyboard.GetState();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Bangers");
        _heartTexture = Content.Load<Texture2D>("HeartPixelArt");
        _sounds.LoadContent(Content);
        _player.LoadContent(Content);
        _mobManager.LoadContent(Content);
        _projectileManager.LoadContent(Content);

        // Laying out the title screen needs the font and the player texture.
        ShowTitle();
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();

        // Closing this way mid-run still keeps the run, as OnExiting saves it.
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            Exit();

        switch (_state)
        {
            case GameState.Start:
                if (WasJustPressed(keyboard, Keys.Escape)) Exit();
                else if (WasJustPressed(keyboard, Keys.Enter)) StartRun();
                else if (_save.Run is not null && WasJustPressed(keyboard, Keys.C)) ContinueRun();
                break;

            case GameState.Playing:
                // Clicking away from the window counts as a pause, so the run
                // is not lost while nobody is looking at it.
                if (WasJustPressed(keyboard, Keys.Escape) || !IsActive) Pause();
                else UpdatePlaying(gameTime, keyboard);
                break;

            case GameState.Paused:
                if (WasJustPressed(keyboard, Keys.Escape)) Resume();
                else if (WasJustPressed(keyboard, Keys.S)) SaveAndQuit();
                break;

            case GameState.GameOver:
                // Another run can beat the time just set.
                if (WasJustPressed(keyboard, Keys.Escape)) Exit();
                else if (WasJustPressed(keyboard, Keys.Enter)) StartRun();
                break;
        }

        // Outside the other states, so the burst and jolt from a killing blow
        // play out over the game over screen instead of freezing on it. A pause
        // is the one place they should freeze.
        if (_state != GameState.Paused)
        {
            _particles.Update(gameTime);
            _screenShake.Update(gameTime);
        }

        _previousKeyboard = keyboard;

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColor);

        Matrix shake = _screenShake.Transform;

        // The world, which shakes.
        _spriteBatch.Begin(transformMatrix: shake);

        // The floor first, so everything else stands on it.
        _tilemap.Draw(_spriteBatch);

        if (_state is GameState.Playing or GameState.Paused)
        {
            _mobManager.Draw(gameTime, _spriteBatch);
            _projectileManager.Draw(gameTime, _spriteBatch);
        }

        // The player is on screen in every state.
        _player.Draw(gameTime, _spriteBatch);

        _spriteBatch.End();

        // Particles, shaken the same so they stay put against the world, and in
        // a batch of their own as they are the only thing blended additively.
        _spriteBatch.Begin(blendState: BlendState.Additive, transformMatrix: shake);
        _particles.Draw(_spriteBatch);
        _spriteBatch.End();

        // Text and hearts, on top of everything and held still to stay readable.
        _spriteBatch.Begin();

        if (_state is GameState.Playing or GameState.Paused)
        {
            DrawRunTimer();
            DrawHealth();
        }

        if (_state == GameState.Paused)
            _spriteBatch.Draw(_pixel, GraphicsDevice.Viewport.Bounds, Color.Black * PauseDim);

        if (_state != GameState.Playing)
        {
            foreach ((string text, Vector2 position, Color color) in _promptLines)
                _spriteBatch.DrawString(_font, text, position, color);
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void UpdatePlaying(GameTime gameTime, KeyboardState keyboard)
    {
        // Counted before the frame is resolved, so the frame a player dies on
        // still counts towards the time they survived.
        _runTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

        _player.AimAt(Mouse.GetState().Position.ToVector2());
        _player.Update(gameTime);

        HandleWeapons(keyboard);
        ResolveProjectileHits();

        // Settle contacts before the manager moves and prunes, so a mob killed
        // here is gone from the list before it can be drawn again.
        foreach (Mob mob in _mobManager.Mobs)
        {
            // Skip anything already killed this frame, so cutting a mob down as
            // it closes in does not still cost a life.
            if (!mob.IsAlive || !mob.Bounds.CollidesWith(_player.Bounds)) continue;

            mob.Kill();
            _sounds.PlayEnemyDeath();
            ShowEnemyDeath(mob);

            // Burnt into the ground where it got through, over the stain its
            // death just left.
            _tilemap.Mark(mob.Position, Tile.Scorched);

            _player.Touch();
            _sounds.PlayDamage();
            _particles.Burst(_player.Position, PlayerDamageColor, PlayerDamageCount, PlayerDamageSpeed, PlayerDamageSize);
            _screenShake.Add(PlayerDamageShake);
        }

        if (!_player.IsAlive)
        {
            FinishRun();
            return;
        }

        _mobManager.Update(gameTime, _player.Position, _runTime);
        _projectileManager.Update(gameTime, GraphicsDevice.Viewport.Bounds);
    }

    // Both weapons fire on the press rather than while held, so one tap is one shot.
    private void HandleWeapons(KeyboardState keyboard)
    {
        // The noise follows the shot, not the key, so a press that lands on
        // the cooldown stays quiet.
        if (WasJustPressed(keyboard, Keys.Space) && _player.TryFire(_projectileManager))
        {
            _sounds.PlayShot();
            _particles.Cone(_player.Muzzle, _player.Rotation, MuzzleSpread, MuzzleColor, MuzzleCount, MuzzleSpeed, MuzzleSize);
            _screenShake.Add(ShotShake);
        }

        if (WasJustPressed(keyboard, Keys.Q) && _player.TryMelee(out MeleeStrike strike))
        {
            _sounds.PlayMelee();

            foreach (Mob mob in _mobManager.Mobs)
            {
                if (strike.Area.CollidesWith(mob.Bounds))
                    Hurt(mob, strike.Damage);
            }
        }
    }

    // Damage and the noises and effects that go with it, in one place so a hit
    // sounds and looks the same however it was dealt.
    private void Hurt(Mob mob, int damage)
    {
        // Already down: no second hit, and no second death rattle.
        if (!mob.IsAlive) return;

        mob.TakeDamage(damage);
        _sounds.PlayDamage();
        _particles.Burst(mob.Position, EnemyHitColor, EnemyHitCount, EnemyHitSpeed, EnemyHitSize);

        if (!mob.IsAlive)
        {
            _sounds.PlayEnemyDeath();
            ShowEnemyDeath(mob);
        }
    }

    private void ShowEnemyDeath(Mob mob)
    {
        _particles.Burst(mob.Position, EnemyDeathColor, EnemyDeathCount, EnemyDeathSpeed, EnemyDeathSize);
        _screenShake.Add(EnemyDeathShake);

        // The burst fades; the stain stays, so the floor keeps a record of
        // where the fighting was.
        _tilemap.Mark(mob.Position, Tile.Stained);
    }

    private void ResolveProjectileHits()
    {
        foreach (Projectile projectile in _projectileManager.Projectiles)
        {
            if (!projectile.IsAlive) continue;

            foreach (Mob mob in _mobManager.Mobs)
            {
                if (!mob.IsAlive || !projectile.Bounds.CollidesWith(mob.Bounds)) continue;

                Hurt(mob, projectile.Damage);

                // A round is spent on the first thing it hits.
                projectile.Kill();
                break;
            }
        }
    }

    private bool WasJustPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    private void StartRun()
    {
        ResetForRun();
        _runTime = 0f;
        _player.Revive();
        _mobManager.Reset();
        _projectileManager.Reset();
        _tilemap.Generate(Environment.TickCount);
    }

    // Picks the saved run up exactly where it was left: the same time on the
    // clock, the same mobs closing in, the same shots in the air and the same
    // marks on the floor.
    private void ContinueRun()
    {
        RunSave run = _save.Run;

        ResetForRun();
        _runTime = run.RunTime;
        _player.Restore(run.RemainingTouches, run.Rotation);
        _mobManager.Restore(run.Mobs, run.TimeSinceLastSpawn);
        _projectileManager.Restore(run.Projectiles);
        _tilemap.Restore(run.Floor);
    }

    // What every run starts with, new or continued.
    private void ResetForRun()
    {
        _state = GameState.Playing;
        _promptLines.Clear();
        _player.Position = GraphicsDevice.Viewport.Bounds.Center.ToVector2();
        _particles.Reset();
        _screenShake.Reset();
        _sounds.StartMusic();
    }

    private void Pause()
    {
        _state = GameState.Paused;
        _sounds.PauseMusic();
        ShowPausePrompt(PausedMessage, ResumeMessage, SaveAndQuitMessage);
    }

    private void Resume()
    {
        _state = GameState.Playing;
        _promptLines.Clear();
        _sounds.ResumeMusic();
    }

    // Only quits once the run is safely on disk; otherwise it says so and
    // stays paused, so a failed save never throws the run away.
    private void SaveAndQuit()
    {
        if (SaveRun()) Exit();
        else ShowPausePrompt(PausedMessage, ResumeMessage, SaveFailedMessage);
    }

    private bool SaveRun()
    {
        _save.Run = new RunSave(
            _runTime,
            _player.RemainingTouches,
            _player.Rotation,
            _mobManager.TimeSinceLastSpawn,
            _mobManager.Capture(),
            _projectileManager.Capture(),
            _tilemap.Capture()
        );

        return SaveFile.Write(_save);
    }

    // However the window is closed - Alt+F4, the close button, the gamepad -
    // a run still in progress is saved rather than lost.
    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        if (_state is GameState.Playing or GameState.Paused) SaveRun();

        base.OnExiting(sender, args);
    }

    private void FinishRun()
    {
        _state = GameState.GameOver;
        _sounds.StopMusic();

        // Every run is kept, not just the good ones, so the leaderboard is
        // always ranked from the whole history.
        RunResult result = new(_runTime, DateTime.Now);
        _save.Results.Add(result);

        // A run that has ended cannot be continued, so the saved one goes.
        _save.Run = null;
        SaveFile.Write(_save);

        List<string> lines = [GameOverMessage, $"Time {FormatTime(_runTime)}", LeaderboardTitle];
        int highlighted = -1;

        List<RunResult> top = _save.TopResults(LeaderboardSize);
        for (int rank = 0; rank < top.Count; rank++)
        {
            // By reference rather than by value, so a run that ties an older
            // time is not mistaken for it.
            if (ReferenceEquals(top[rank], result)) highlighted = lines.Count;

            lines.Add($"{rank + 1}.  {FormatTime(top[rank].Time)}");
        }

        lines.Add(RestartMessage);

        ShowPrompt(lines, highlighted);
    }

    // The running total, centred along the top edge where it stays clear of
    // the player and whatever is closing in on them.
    private void DrawRunTimer()
    {
        string text = FormatTime(_runTime);
        float centerX = GraphicsDevice.Viewport.Bounds.Center.X;

        _spriteBatch.DrawString(
            _font,
            text,
            new Vector2(centerX - _font.MeasureString(text).X / 2f, TimerMargin),
            Color.White
        );
    }

    // One heart per touch the player started with, filled while they still
    // have it. Tucked into the top left, clear of the timer along the top.
    private void DrawHealth()
    {
        float scale = HeartHeight / _heartTexture.Height;
        float step = _heartTexture.Width * scale + HeartGap;

        for (int i = 0; i < Player.MaxTouches; i++)
        {
            _spriteBatch.Draw(
                texture: _heartTexture,
                position: new Vector2(HeartMargin + step * i, HeartMargin),
                sourceRectangle: null,
                color: i < _player.RemainingTouches ? Color.White : Color.White * LostHeartFade,
                rotation: 0f,
                // Drawn from its top left, so the row starts flush with the margin.
                origin: Vector2.Zero,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 0f
            );
        }
    }

    // Minutes and seconds to a tenth, so the timer can be seen moving.
    private static string FormatTime(float seconds) =>
        TimeSpan.FromSeconds(seconds).ToString(@"m\:ss\.f");

    // Centres the lines and the player sprite together as one block, text on
    // top, so a four line prompt sits as evenly as a one line one.
    // highlightedLine, if given, is drawn in HighlightColor instead of white.
    private void ShowPrompt(IReadOnlyList<string> lines, int highlightedLine = -1)
    {
        Vector2 center = GraphicsDevice.Viewport.Bounds.Center.ToVector2();

        float textHeight = MeasureLines(lines);
        float blockHeight = textHeight + PromptGap + _player.DrawRadius * 2f;
        float top = center.Y - blockHeight / 2f;

        LayOutLines(lines, top, highlightedLine);

        _player.Position = new Vector2(center.X, top + textHeight + PromptGap + _player.DrawRadius);
    }

    // The title screen, offering the saved run when there is one.
    private void ShowTitle()
    {
        List<string> lines = [.. StartMessages];

        if (_save.Run is null)
        {
            lines.Add(StartMessage);
        }
        else
        {
            lines.Add($"C - continue ({FormatTime(_save.Run.RunTime)})");
            lines.Add(NewRunMessage);
        }

        List<RunResult> best = _save.TopResults(1);
        if (best.Count > 0) lines.Add($"Best {FormatTime(best[0].Time)}");

        ShowPrompt(lines);
    }

    // Like ShowPrompt, but stacked above the player where they stand instead
    // of moving them, as mid-run the player is where the fight is.
    private void ShowPausePrompt(params string[] lines)
    {
        float top = _player.Position.Y - _player.DrawRadius - PromptGap - MeasureLines(lines);
        LayOutLines(lines, top);
    }

    private float MeasureLines(IReadOnlyList<string> lines)
    {
        float height = LineGap * (lines.Count - 1);
        foreach (string line in lines) height += _font.MeasureString(line).Y;
        return height;
    }

    // Centres each line across the screen, stacked down from top.
    private void LayOutLines(IReadOnlyList<string> lines, float top, int highlightedLine = -1)
    {
        float centerX = GraphicsDevice.Viewport.Bounds.Center.X;

        _promptLines.Clear();

        float y = top;
        for (int i = 0; i < lines.Count; i++)
        {
            Vector2 size = _font.MeasureString(lines[i]);
            Color color = i == highlightedLine ? HighlightColor : Color.White;

            _promptLines.Add((lines[i], new Vector2(centerX - size.X / 2f, y), color));
            y += size.Y + LineGap;
        }
    }
}
