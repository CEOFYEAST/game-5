using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using game_1.Saving;

namespace game_1.Projectiles;

public class ProjectileManager
{
    private readonly List<Projectile> _projectiles = [];

    // Weapons name their projectile texture, so the same asset is only loaded once.
    private readonly Dictionary<string, Texture2D> _textures = [];

    private ContentManager _content;

    public IReadOnlyList<Projectile> Projectiles => _projectiles;

    public void LoadContent(ContentManager content)
    {
        _content = content;
    }

    // Clears shots left in the air so a new run starts empty.
    public void Reset()
    {
        _projectiles.Clear();
    }

    public void Spawn(string textureName, Vector2 position, Vector2 velocity, int damage, float scale)
    {
        if (!_textures.TryGetValue(textureName, out Texture2D texture))
        {
            texture = _content.Load<Texture2D>(textureName);
            _textures[textureName] = texture;
        }

        _projectiles.Add(new Projectile(textureName, texture, position, velocity, damage, scale));
    }

    public List<ProjectileSave> Capture()
    {
        List<ProjectileSave> saves = [];

        foreach (Projectile projectile in _projectiles)
        {
            if (!projectile.IsAlive) continue;

            saves.Add(new ProjectileSave(
                projectile.TextureName,
                projectile.Position.X,
                projectile.Position.Y,
                projectile.Velocity.X,
                projectile.Velocity.Y,
                projectile.Damage,
                projectile.Scale
            ));
        }

        return saves;
    }

    // Replaces whatever is in the air with the shots from a save.
    public void Restore(IEnumerable<ProjectileSave> saves)
    {
        Reset();
        if (saves is null) return;

        foreach (ProjectileSave save in saves)
        {
            Spawn(
                save.Texture,
                new Vector2(save.X, save.Y),
                new Vector2(save.VelocityX, save.VelocityY),
                save.Damage,
                save.Scale
            );
        }
    }

    public void Update(GameTime gameTime, Rectangle viewport)
    {
        foreach (Projectile projectile in _projectiles)
            projectile.Update(gameTime);

        // Drop spent shots and anything that has left the screen, so they do not
        // fly off forever eating memory.
        _projectiles.RemoveAll(projectile =>
            !projectile.IsAlive || !projectile.Bounds.CollidesWith(ToBounds(viewport)));
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (Projectile projectile in _projectiles)
            projectile.Draw(gameTime, spriteBatch);
    }

    private static Collisions.BoundingRectangle ToBounds(Rectangle rectangle) =>
        new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}
