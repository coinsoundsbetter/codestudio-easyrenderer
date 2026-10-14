using System.Numerics;
using Raylib_cs;

namespace SoftwareRendering;

public struct ClipVertex {
    public Vector4 Pos;
    public Color Color;
    public Vector2 UV;
    public Vector3 Normal; // 世界空间法线，裁剪时与其他属性一起插值。
    public Vector3 WorldPosition;
    public Vector4 Tangent;
}
