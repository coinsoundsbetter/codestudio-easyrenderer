using System.Numerics;

namespace SoftwareRendering;

public class Model {
    public Mesh[] Meshes = [];
    public Matrix4x4 Transform = Matrix4x4.Identity;
} 