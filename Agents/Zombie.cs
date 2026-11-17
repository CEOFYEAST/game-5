using Microsoft.Xna.Framework;
using game_1.Rendering;

namespace game_1.Agents;

public class Zombie : Mob
{
    // Dies to a single hit for now; raise this for a tougher zombie.
    private const int StartingHealth = 1;

    // Distance from the camera. Mobs share one depth, so where two overlap
    // their bodies cut into each other rather than one popping over the other.
    private const float Depth = 500f;

    public Zombie(Vector2 initialPosition) : base(initialPosition, StartingHealth)
    {
        Color = Color.Red;
    }

    protected override float Radius => 16f;

    protected override float Speed => 60f;

    public override void Draw(GameTime gameTime, Renderer3D renderer) =>
        renderer.Draw(renderer.Sphere, Position, Radius, Depth, Matrix.Identity, Color);
}
