using Microsoft.Xna.Framework;
using game_1.Rendering;

namespace game_1.Agents;

public class Tick : Mob
{
    // Dies to a single hit for now; raise this for a tougher tick.
    private const int StartingHealth = 1;

    // Same depth as every other mob; see Zombie.
    private const float Depth = 500f;

    // Radians per second the pyramid turns about its own point.
    private const float SpinSpeed = 2.5f;

    // How far the pyramid leans away from facing the camera, in radians, so its
    // sides catch the light differently as it spins rather than reading as a flat square.
    private const float Tilt = 0.45f;

    public Tick(Vector2 initialPosition) : base(initialPosition, StartingHealth)
    {
        Color = Color.Goldenrod;
    }

    protected override float Radius => 9.6f;

    // Ticks close in twice as fast as zombies.
    protected override float Speed => 120f;

    public override void Draw(GameTime gameTime, Renderer3D renderer)
    {
        float spin = (float)gameTime.TotalGameTime.TotalSeconds * SpinSpeed;
        Matrix rotation = Matrix.CreateRotationZ(spin) * Matrix.CreateRotationX(Tilt);

        renderer.Draw(renderer.Pyramid, Position, Radius, Depth, rotation, Color);
    }
}
