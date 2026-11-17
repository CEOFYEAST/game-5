using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using game_1.Collisions;
using game_1.Rendering;

namespace game_1.Agents;

public abstract class Agent
{
    protected Agent(Vector2 initialPosition)
    {
        Position = initialPosition;
    }

    // Centre of the body, in screen space.
    public Vector2 Position { get; set; }

    // The colour the body's mesh is drawn in.
    public Color Color { get; set; } = Color.White;

    // Size of the body in pixels. The mesh is drawn to fill exactly this
    // radius, so what is seen and what collides are the same circle.
    protected abstract float Radius { get; }

    // How far the body reaches from Position. Used to place spawns fully
    // off-screen and to lay out the title screen around the player.
    public float DrawRadius => Radius;

    // Derived from Position, so they can never go stale.
    public BoundingCircle Bounds => new(Position, Radius);

    // Bodies are built in code, so most agents have nothing to load.
    public virtual void LoadContent(ContentManager content) { }

    public abstract void Draw(GameTime gameTime, Renderer3D renderer);
}
