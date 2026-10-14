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
    public Vector3 Normal; // 模型输入时为模型空间；屏幕顶点中保存世界空间法线。
    public Vector3 WorldPosition; // 屏幕顶点仍保留世界位置，供高光计算观察方向。
    public Vector4 Tangent; // xyz 为切线方向，w 为 TBN 手性（+1/-1）。
}
