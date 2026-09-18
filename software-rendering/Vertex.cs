using System.Numerics;
using Raylib_cs;

namespace SoftwareRendering;

public struct Vertex {
    public float X;
    public float Y;
    public float Z;
    public Color Color;
    public Vector2 UV;
    public float InvW;
}
