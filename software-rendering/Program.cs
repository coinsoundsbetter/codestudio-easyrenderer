using System.Numerics;
using Assimp;
using Raylib_cs;

namespace SoftwareRendering;

internal static class Program
{
    [STAThread]
    private static void Main() {
        var width = 800;
        var height = 600;
        Raylib.InitWindow(width, height, "Software Rendering");
        Raylib.SetTargetFPS(60);

        var frameBuffer = new FrameBuffer(width, height);
        var depthBuffer = new DepthBuffer(width, height);

        var model = LoadModel(@"F:\codestudio-easyrenderer\software-rendering\Assets\Models\box.fbx");

        while (!Raylib.WindowShouldClose()) {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);

            depthBuffer.Clear(float.MaxValue);
            
            DrawModel(model, width, height);

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    var pixel = frameBuffer.GetPixel(x, y);
                    Raylib.DrawPixel(x, y, pixel);
                }
            }

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    private static void DrawModel(Model model, int screenWidth, int screenHeight) {
        //模型空间
        var m = Matrix4x4.Identity;
        //观察空间
        var cameraPos = new Vector3(0, 0, -10f);
        var cameraTarget = Vector3.Zero;
        var cameraUp = Vector3.UnitY;
        var view = Matrix4x4.CreateLookAt(cameraPos, cameraTarget, cameraUp);
        //透视投影
        var fov = MathF.PI / 3f; //60°
        var aspectRatio = (float)screenWidth / screenHeight;
        var nearPlane = 0.1f;
        var farPlane = 100f;
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspectRatio, nearPlane, farPlane);
        //组合矩阵
        var mvp = model.Transform * view * projection;
        for (int i = 0; i < model.Meshes.Length; i++) {
            var mesh = model.Meshes[i];
            for (int j = 0; j < mesh.Vertices.Length; j++) {
                var vertex = mesh.Vertices[j];
                var clipPos = Vector4.Transform(new Vector4(vertex.X, vertex.Y, vertex.Z, 1f), mvp);
                var ndc = new Vector3(
                    clipPos.X / clipPos.W,
                    clipPos.Y / clipPos.W,
                    clipPos.Z / clipPos.W);
            }
        }
    }

    private static Model LoadModel(string path) {
        if (!File.Exists(path)) {
            throw new FileNotFoundException("模型文件不存在。", path);
        }

        using var importer = new AssimpContext();

        var scene = importer.ImportFile(
            path,
            PostProcessSteps.Triangulate |
            PostProcessSteps.PreTransformVertices
        );

        var meshes = new Mesh[scene.MeshCount];

        for (int i = 0; i < scene.MeshCount; i++) {
            meshes[i] = ConvertToMesh(scene.Meshes[i]);
        }

        return new Model {
            Meshes = meshes,
            Transform = System.Numerics.Matrix4x4.Identity
        };
    }

    private static Mesh ConvertToMesh(Assimp.Mesh source) {
        var vertices = new Vertex[source.VertexCount];
        for (int i = 0; i < source.VertexCount; i++) {
            var position = source.Vertices[i];
            vertices[i] = new Vertex() {
                X = position.X,
                Y = position.Y,
                Z = position.Z,
                Color = Color.White,
            };
        }

        var indices = new List<int>();
        foreach (var face in source.Faces) {
            if (face.IndexCount != 3) {
                continue;
            }

            for (int i = 0; i < 3; i++) {
                var index = face.Indices[i];
                if (index < 0 || index >= vertices.Length) {
                    throw new InvalidDataException();
                }
                
                indices.Add(index);
            }
        }

        return new Mesh() {
            Vertices = vertices,
            Indices = indices.ToArray(),
        };
    }
}
