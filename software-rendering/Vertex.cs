using System.Numerics;
using Raylib_cs;

namespace SoftwareRendering;

public struct Vertex {
    public float X;
    public float Y;
    public float Z;
    public Color Color;
    public Vector2 UV;
    // 透视除法前裁剪空间 W 的倒数，用于透视正确的顶点属性插值。
    public float InvW;
}
