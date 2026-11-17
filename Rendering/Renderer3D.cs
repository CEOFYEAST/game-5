using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace game_1.Rendering;

// Draws lit 3D meshes into the same pixel space the sprites use. An
// orthographic camera maps one unit to one screen pixel, so a mesh drawn at an
// agent's Position with its radius lines up exactly with its collision bounds,
// and nothing else in the game has to know the agents are 3D.
public sealed class Renderer3D
{
    // How much of the view depth there is to place meshes in. Meshes are drawn
    // at a distance from the camera somewhere between the two.
    private const float NearPlane = 1f;
    private const float FarPlane = 2000f;

    private static readonly RasterizerState NoCulling = new() { CullMode = CullMode.None };

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;

    private Matrix _shake = Matrix.Identity;

    public Renderer3D(GraphicsDevice device)
    {
        _device = device;

        Sphere = Mesh.CreateSphere(device);
        Pyramid = Mesh.CreatePyramid(device);

        _effect = new BasicEffect(device)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            AmbientLightColor = new Vector3(0.25f),
            SpecularColor = new Vector3(0.45f),
            SpecularPower = 24f,
            View = Matrix.Identity
        };

        // One key light from the upper left, shining down the screen and in
        // towards it. +Y is down the screen, so a positive Y here lights from above.
        _effect.DirectionalLight0.Enabled = true;
        _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(0.6f, 0.8f, -1f));
        _effect.DirectionalLight0.DiffuseColor = new Vector3(0.9f);
        _effect.DirectionalLight0.SpecularColor = new Vector3(1f);

        // A faint fill from the opposite side, so shadowed edges keep some shape
        // against the dark floor.
        _effect.DirectionalLight1.Enabled = true;
        _effect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(-0.6f, -0.3f, -0.4f));
        _effect.DirectionalLight1.DiffuseColor = new Vector3(0.2f);
        _effect.DirectionalLight1.SpecularColor = Vector3.Zero;

        _effect.DirectionalLight2.Enabled = false;
    }

    public Mesh Sphere { get; }

    public Mesh Pyramid { get; }

    // Call before drawing meshes, and again after any SpriteBatch in between,
    // as SpriteBatch switches depth testing off. shake is the same transform
    // the sprites are drawn with, so meshes jolt along with the world.
    public void Begin(Matrix shake)
    {
        _shake = shake;

        _device.DepthStencilState = DepthStencilState.Default;
        _device.BlendState = BlendState.Opaque;

        // The camera flips Y to match screen space, which turns every triangle's
        // winding inside out. Depth testing alone keeps closed meshes correct,
        // so culling is simply left off.
        _device.RasterizerState = NoCulling;
    }

    // position and radius are in screen pixels. depth is the distance from the
    // camera: a smaller depth draws in front. rotation turns the mesh about its
    // own centre before it is placed.
    public void Draw(Mesh mesh, Vector2 position, float radius, float depth, Matrix rotation, Color color)
    {
        Viewport viewport = _device.Viewport;

        // The mesh sits straight in front of the camera, and the move to its
        // screen position happens after the view. That keeps the camera looking
        // dead on at every mesh, so highlights land the same way on each of them
        // wherever they are on screen.
        _effect.World = rotation * Matrix.CreateScale(radius) * Matrix.CreateTranslation(0f, 0f, -depth);
        _effect.Projection =
            Matrix.CreateTranslation(position.X, position.Y, 0f)
            * _shake
            * Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, NearPlane, FarPlane);

        _effect.DiffuseColor = color.ToVector3();

        mesh.Draw(_device, _effect);
    }
}
