using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using game_1.Saving;

namespace game_1.World;

// What a cell of the floor shows. Ordered by how much a mark matters: a later
// mark only ever replaces an earlier one, so a scorch is never painted over by
// a stain, and the tile values double as their place in the atlas.
public enum Tile : byte
{
    // No mark: the generated floor shows through.
    None = 0,

    Ground,

    Cracked,

    // Left wherever a mob goes down.
    Stained,

    // Left wherever a mob got through to the player.
    Scorched
}

// The floor of the arena, drawn under everything else. It has two layers: a
// base layer grown from a single seed, so it never needs saving, and a layer of
// marks the fight leaves behind, which does. The tile art is painted in code
// when the map is made, so the floor needs no assets.
public class Tilemap
{
    public const int TileSize = 32;

    // One spare ring of tiles past every edge of the screen. Screen shake tops
    // out well under a tile, so the floor never pulls far enough to show the
    // clear colour behind it.
    private const int Border = 1;

    // Out of every 1024 base tiles, how many come out cracked.
    private const int CrackedPer1024 = 170;

    // Each tile is darkened by up to this share of itself, so a floor made of
    // one ground tile repeated still reads as uneven ground rather than a grid.
    private const float MaxDarkening = 0.12f;

    // The atlas is painted from a fixed seed, so the tiles look the same every
    // launch and only the way they are laid out changes from run to run.
    private const int AtlasSeed = 4;

    private const int TileKinds = 4;

    // Kept close to the clear colour, as particles are drawn additively and
    // wash out on anything bright. The floor sits behind them, not over them.
    private static readonly Color GroundColor = new(31, 32, 41);
    private static readonly Color CrackColor = new(17, 17, 23);
    private static readonly Color StainCoreColor = new(46, 8, 14);
    private static readonly Color StainRimColor = new(76, 19, 24);
    private static readonly Color ScorchCoreColor = new(10, 9, 11);
    private static readonly Color EmberColor = new(92, 38, 16);

    private readonly Texture2D _atlas;

    private readonly int _columns;

    private readonly int _rows;

    private readonly Tile[] _base;

    private readonly Tile[] _marks;

    public Tilemap(GraphicsDevice graphicsDevice, Rectangle viewport)
    {
        _columns = (int)MathF.Ceiling(viewport.Width / (float)TileSize) + Border * 2;
        _rows = (int)MathF.Ceiling(viewport.Height / (float)TileSize) + Border * 2;
        _base = new Tile[_columns * _rows];
        _marks = new Tile[_columns * _rows];
        _atlas = BuildAtlas(graphicsDevice);

        Generate(Environment.TickCount);
    }

    // The number the base layer is grown from. The same seed always grows the
    // same floor, which is all a save needs to put it back.
    public int Seed { get; private set; }

    // Lays out a fresh floor with nothing on it yet.
    public void Generate(int seed)
    {
        Seed = seed;

        for (int row = 0; row < _rows; row++)
        {
            for (int column = 0; column < _columns; column++)
            {
                bool cracked = (Hash(column, row, seed) & 1023) < CrackedPer1024;
                _base[row * _columns + column] = cracked ? Tile.Cracked : Tile.Ground;
            }
        }

        Array.Clear(_marks);
    }

    // Marks the tile under a point in screen space. Points off the floor, and
    // marks that matter less than the one already there, are ignored.
    public void Mark(Vector2 position, Tile tile)
    {
        int column = (int)MathF.Floor(position.X / TileSize) + Border;
        int row = (int)MathF.Floor(position.Y / TileSize) + Border;

        if (column < 0 || column >= _columns || row < 0 || row >= _rows) return;

        int index = row * _columns + column;
        if (tile > _marks[index]) _marks[index] = tile;
    }

    public TilemapSave Capture()
    {
        byte[] marks = new byte[_marks.Length];
        for (int i = 0; i < _marks.Length; i++) marks[i] = (byte)_marks[i];

        return new TilemapSave(Seed, _columns, _rows, marks);
    }

    // Grows the saved floor again and puts its marks back. Marks saved from a
    // window of a different size would land in the wrong places, so then only
    // the floor itself comes back.
    public void Restore(TilemapSave save)
    {
        if (save is null) return;

        Generate(save.Seed);

        if (save.Columns != _columns || save.Rows != _rows || save.Marks?.Length != _marks.Length)
            return;

        for (int i = 0; i < _marks.Length; i++)
        {
            Tile mark = (Tile)save.Marks[i];
            _marks[i] = mark <= Tile.Scorched ? mark : Tile.None;
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        for (int row = 0; row < _rows; row++)
        {
            for (int column = 0; column < _columns; column++)
            {
                int index = row * _columns + column;
                Tile tile = _marks[index] != Tile.None ? _marks[index] : _base[index];

                // Flips and shading come from the same hash as the layout, so
                // they need no storing and a restored floor matches exactly.
                uint hash = Hash(column, row, Seed);
                SpriteEffects flip = (SpriteEffects)((hash >> 10) & 3);
                float shade = 1f - ((hash >> 12) & 255) / 255f * MaxDarkening;

                spriteBatch.Draw(
                    texture: _atlas,
                    position: new Vector2((column - Border) * TileSize, (row - Border) * TileSize),
                    sourceRectangle: SourceOf(tile),
                    color: new Color(shade, shade, shade),
                    rotation: 0f,
                    origin: Vector2.Zero,
                    scale: 1f,
                    effects: flip,
                    layerDepth: 0f
                );
            }
        }
    }

    private static Rectangle SourceOf(Tile tile) =>
        new(((int)tile - 1) * TileSize, 0, TileSize, TileSize);

    // Scrambles a cell and the seed into a number that looks random but is the
    // same every time it is asked for.
    private static uint Hash(int column, int row, int seed)
    {
        unchecked
        {
            uint hash = (uint)seed;
            hash ^= (uint)column * 0x27D4EB2Du;
            hash = (hash ^ (hash >> 15)) * 0x85EBCA6Bu;
            hash ^= (uint)row * 0x165667B1u;
            hash = (hash ^ (hash >> 13)) * 0xC2B2AE35u;
            return hash ^ (hash >> 16);
        }
    }

    // Paints every tile side by side into one texture, in Tile order, so the
    // whole floor draws from a single texture.
    private static Texture2D BuildAtlas(GraphicsDevice graphicsDevice)
    {
        Random random = new(AtlasSeed);
        int width = TileSize * TileKinds;
        Color[] atlas = new Color[width * TileSize];

        for (int kind = 0; kind < TileKinds; kind++)
        {
            Color[] tile = (Tile)(kind + 1) switch
            {
                Tile.Cracked => PaintCracked(random),
                Tile.Stained => PaintStained(random),
                Tile.Scorched => PaintScorched(random),
                _ => PaintGround(random)
            };

            for (int y = 0; y < TileSize; y++)
                Array.Copy(tile, y * TileSize, atlas, y * width + kind * TileSize, TileSize);
        }

        Texture2D texture = new(graphicsDevice, width, TileSize);
        texture.SetData(atlas);
        return texture;
    }

    // Packed dirt: a flat colour with a little grain and the odd dark speck.
    private static Color[] PaintGround(Random random)
    {
        Color[] pixels = new Color[TileSize * TileSize];

        for (int i = 0; i < pixels.Length; i++)
        {
            int grain = random.Next(-3, 4);
            if (random.NextDouble() < 0.04) grain -= 6;

            pixels[i] = Shift(GroundColor, grain);
        }

        return pixels;
    }

    // Ground with a couple of cracks wandering across it.
    private static Color[] PaintCracked(Random random)
    {
        Color[] pixels = PaintGround(random);

        for (int crack = 0; crack < 2; crack++)
        {
            Vector2 point = new(random.Next(4, TileSize - 4), random.Next(4, TileSize - 4));
            float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
            int length = random.Next(14, 24);

            for (int step = 0; step < length; step++)
            {
                int x = (int)point.X;
                int y = (int)point.Y;
                if (x < 0 || x >= TileSize || y < 0 || y >= TileSize) break;

                pixels[y * TileSize + x] = CrackColor;

                // Now and then a crack thickens for a pixel, so it is not a
                // perfectly even line.
                if (random.NextDouble() < 0.3 && x + 1 < TileSize)
                    pixels[y * TileSize + x + 1] = CrackColor;

                angle += (float)(random.NextDouble() - 0.5) * 1.1f;
                point += new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
        }

        return pixels;
    }

    // Ground with a dark red blot and a few drops thrown off it. Kept clear of
    // the tile's edges, so neighbouring stains never show a hard seam.
    private static Color[] PaintStained(Random random)
    {
        Color[] pixels = PaintGround(random);
        Vector2 center = new(TileSize / 2f + random.Next(-2, 3), TileSize / 2f + random.Next(-2, 3));
        float radius = 8f;
        float phaseA = (float)(random.NextDouble() * MathHelper.TwoPi);
        float phaseB = (float)(random.NextDouble() * MathHelper.TwoPi);

        PaintBlob(pixels, center, pixel =>
        {
            Vector2 offset = pixel - center;
            float angle = MathF.Atan2(offset.Y, offset.X);
            float edge = radius * (1f + 0.22f * MathF.Sin(3f * angle + phaseA) + 0.12f * MathF.Sin(5f * angle + phaseB));
            float t = offset.Length() / edge;

            return t < 1f ? Color.Lerp(StainCoreColor, StainRimColor, t * t) : null;
        });

        for (int drop = 0; drop < 5; drop++)
        {
            float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
            float distance = 9f + (float)random.NextDouble() * 3f;
            Vector2 dropCenter = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            float dropRadius = 0.8f + (float)random.NextDouble() * 0.7f;

            PaintBlob(pixels, dropCenter, pixel =>
                Vector2.Distance(pixel, dropCenter) < dropRadius ? StainRimColor : null);
        }

        return pixels;
    }

    // A burnt patch: black at the heart, fading out into the ground, with a few
    // embers still glowing in a ring around it.
    private static Color[] PaintScorched(Random random)
    {
        Color[] pixels = PaintGround(random);
        Vector2 center = new(TileSize / 2f, TileSize / 2f);
        float radius = 13f;
        float phase = (float)(random.NextDouble() * MathHelper.TwoPi);

        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                Vector2 offset = new Vector2(x + 0.5f, y + 0.5f) - center;
                float angle = MathF.Atan2(offset.Y, offset.X);
                float edge = radius * (1f + 0.15f * MathF.Sin(4f * angle + phase));
                float t = offset.Length() / edge;
                if (t >= 1f) continue;

                int index = y * TileSize + x;
                Color burnt = Color.Lerp(ScorchCoreColor, pixels[index], t * t);

                if (t > 0.5f && t < 0.8f && random.NextDouble() < 0.18)
                    burnt = Color.Lerp(burnt, EmberColor, 0.6f);

                pixels[index] = burnt;
            }
        }

        return pixels;
    }

    // Paints every pixel the shape says to, over what is already there, within
    // a box around center big enough for anything painted here.
    private static void PaintBlob(Color[] pixels, Vector2 center, Func<Vector2, Color?> shape)
    {
        const int Reach = 13;

        for (int y = (int)center.Y - Reach; y <= (int)center.Y + Reach; y++)
        {
            for (int x = (int)center.X - Reach; x <= (int)center.X + Reach; x++)
            {
                if (x < 0 || x >= TileSize || y < 0 || y >= TileSize) continue;

                Color? color = shape(new Vector2(x + 0.5f, y + 0.5f));
                if (color.HasValue) pixels[y * TileSize + x] = color.Value;
            }
        }
    }

    private static Color Shift(Color color, int amount) => new(
        Math.Clamp(color.R + amount, 0, 255),
        Math.Clamp(color.G + amount, 0, 255),
        Math.Clamp(color.B + amount, 0, 255)
    );
}
