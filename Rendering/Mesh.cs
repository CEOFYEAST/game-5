using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace game_1.Rendering;

// A shape built entirely in code and kept on the graphics card. Every mesh is
// made to fit inside a sphere of radius 1 around its origin, so scaling it by
// a radius in pixels makes it exactly that big on screen.
//
// +Z points out of the screen towards the camera, and +Y points down the
// screen, matching the 2D coordinates the rest of the game uses.
public sealed class Mesh
{
    private readonly VertexBuffer _vertices;
    private readonly IndexBuffer _indices;
    private readonly int _triangleCount;

    private Mesh(GraphicsDevice device, VertexPositionNormalTexture[] vertices, short[] indices)
    {
        _vertices = new VertexBuffer(device, typeof(VertexPositionNormalTexture), vertices.Length, BufferUsage.WriteOnly);
        _vertices.SetData(vertices);

        _indices = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _indices.SetData(indices);

        _triangleCount = indices.Length / 3;
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        device.SetVertexBuffer(_vertices);
        device.Indices = _indices;

        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _triangleCount);
        }
    }

    // A UV sphere: rings of latitude from the pole facing the camera round to
    // the one facing away. Each vertex's normal is just its position, since on
    // a unit sphere that already points straight out, which gives smooth shading.
    public static Mesh CreateSphere(GraphicsDevice device, int slices = 32, int stacks = 20)
    {
        List<VertexPositionNormalTexture> vertices = [];

        for (int stack = 0; stack <= stacks; stack++)
        {
            float phi = MathHelper.Pi * stack / stacks;
            float z = MathF.Cos(phi);
            float ring = MathF.Sin(phi);

            // One more vertex than slices so the seam closes on a duplicate.
            for (int slice = 0; slice <= slices; slice++)
            {
                float theta = MathHelper.TwoPi * slice / slices;
                Vector3 point = new(ring * MathF.Cos(theta), ring * MathF.Sin(theta), z);
                vertices.Add(new VertexPositionNormalTexture(point, point, Vector2.Zero));
            }
        }

        List<short> indices = [];
        int rowLength = slices + 1;

        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < slices; slice++)
            {
                short a = (short)(stack * rowLength + slice);
                short b = (short)(a + rowLength);

                indices.AddRange([a, b, (short)(a + 1)]);
                indices.AddRange([(short)(a + 1), b, (short)(b + 1)]);
            }
        }

        return new Mesh(device, [.. vertices], [.. indices]);
    }

    // A square-based pyramid with its tip towards the camera. Each face gets
    // its own copies of its corners so it can carry its own flat normal, which
    // is what gives the faces their hard, distinct shading.
    public static Mesh CreatePyramid(GraphicsDevice device)
    {
        // Base corners on the unit circle and the tip on the axis, sat so the
        // whole shape stays inside the unit sphere.
        const float baseZ = -0.4f;
        const float tipZ = 0.8f;

        Vector3[] corners =
        [
            new(1f, 0f, baseZ),
            new(0f, 1f, baseZ),
            new(-1f, 0f, baseZ),
            new(0f, -1f, baseZ)
        ];
        Vector3 tip = new(0f, 0f, tipZ);
        Vector3 centre = new(0f, 0f, (baseZ * 4f + tipZ) / 5f);

        List<VertexPositionNormalTexture> vertices = [];
        List<short> indices = [];

        void AddFace(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));

            // Point the normal away from the middle of the shape, whichever way
            // round the corners were given.
            if (Vector3.Dot(normal, (a + b + c) / 3f - centre) < 0f)
                normal = -normal;

            short first = (short)vertices.Count;
            vertices.Add(new VertexPositionNormalTexture(a, normal, Vector2.Zero));
            vertices.Add(new VertexPositionNormalTexture(b, normal, Vector2.Zero));
            vertices.Add(new VertexPositionNormalTexture(c, normal, Vector2.Zero));
            indices.AddRange([first, (short)(first + 1), (short)(first + 2)]);
        }

        for (int i = 0; i < corners.Length; i++)
            AddFace(corners[i], corners[(i + 1) % corners.Length], tip);

        // The base, as two triangles.
        AddFace(corners[0], corners[1], corners[2]);
        AddFace(corners[0], corners[2], corners[3]);

        return new Mesh(device, [.. vertices], [.. indices]);
    }
}
