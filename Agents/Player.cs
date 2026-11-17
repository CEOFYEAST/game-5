using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using game_1.Projectiles;
using game_1.Rendering;
using game_1.Weapons;

namespace game_1.Agents;

public class Player : Agent
{
    // Enemy touches a player survives; the third one kills them. Public so the
    // health display knows how many hearts to lay out.
    public const int MaxTouches = 3;

    // Extra clearance so a shot starts clear of the player's own body.
    private const float MuzzleGap = 4f;

    // Distance from the camera: nearer than the mobs, so the player is always
    // drawn on top of anything touching them.
    private const float Depth = 400f;

    public Player(Vector2 initialPosition) : base(initialPosition)
    {
        Color = Color.RoyalBlue;
    }

    protected override float Radius => 32f;

    // Touches left before the player dies.
    public int RemainingTouches { get; private set; } = MaxTouches;

    public bool IsAlive => RemainingTouches > 0;

    // Where the player is aiming, in radians. 0 points right, matching Atan2.
    public float Rotation { get; private set; }

    // Always carried, so there is something to swing when a mob gets too close.
    public MeleeWeapon Melee { get; } = new();

    public RangedWeapon EquippedWeapon { get; private set; } = new Pistol();

    // The point shots leave from: just past the body, along the aim.
    public Vector2 Muzzle => Position + Facing * (DrawRadius + MuzzleGap);

    private Vector2 Facing => new(MathF.Cos(Rotation), MathF.Sin(Rotation));

    public void Equip(RangedWeapon weapon) => EquippedWeapon = weapon;

    // Turns to face a point; the mouse only ever rotates the player.
    public void AimAt(Vector2 target)
    {
        Vector2 toTarget = target - Position;

        // Hold the last angle when the mouse sits exactly on the player,
        // rather than snapping to the right.
        if (toTarget == Vector2.Zero) return;

        Rotation = MathF.Atan2(toTarget.Y, toTarget.X);
    }

    // Ticks weapon cooldowns down.
    public void Update(GameTime gameTime)
    {
        Melee.Update(gameTime);
        EquippedWeapon.Update(gameTime);
    }

    public bool TryFire(ProjectileManager projectiles) =>
        EquippedWeapon.TryFire(Muzzle, Rotation, projectiles);

    public bool TryMelee(out MeleeStrike strike) =>
        Melee.TryStrike(Position, Rotation, out strike);

    public void Touch()
    {
        if (RemainingTouches > 0) RemainingTouches--;
    }

    // Puts the player back to full health for a fresh run.
    public void Revive()
    {
        RemainingTouches = MaxTouches;
        Melee.ClearStrike();
    }

    // Picks a saved run back up. A run is only ever saved while the player is
    // alive, so at least one touch is always left.
    public void Restore(int remainingTouches, float rotation)
    {
        Revive();
        RemainingTouches = Math.Clamp(remainingTouches, 1, MaxTouches);
        Rotation = rotation;
    }

    public override void LoadContent(ContentManager content)
    {
        Melee.LoadContent(content);
    }

    // The body only. The melee swing is a sprite, so the game draws it
    // separately, before this, to keep it underneath.
    public override void Draw(GameTime gameTime, Renderer3D renderer) =>
        renderer.Draw(renderer.Sphere, Position, Radius, Depth, Matrix.Identity, Color);
}
