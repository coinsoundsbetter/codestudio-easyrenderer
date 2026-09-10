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

        var modelPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Models",
            "box.fbx");
        var model = LoadModel(modelPath);

        var modelZ = 0f;
        const float modelMoveSpeed = 1f;

        while (!Raylib.WindowShouldClose()) {

            var delta = Raylib.GetFrameTime();
            if (Raylib.IsKeyDown(KeyboardKey.S)) {
                modelZ -= modelMoveSpeed * delta;
            }
            if (Raylib.IsKeyDown(KeyboardKey.W)) {
                modelZ += modelMoveSpeed * delta;
            }

            model.Transform = Matrix4x4.CreateTranslation(1f, 0f, modelZ);
            
            Raylib.BeginDrawing();

            frameBuffer.Clear(Color.Black);
            depthBuffer.Clear(float.MaxValue);
            
            DrawModel(frameBuffer, depthBuffer, model, width, height);

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

    private static void DrawModel(FrameBuffer frameBuffer, DepthBuffer depthBuffer, Model model, int screenWidth, int screenHeight) {
        //模型空间
        /*var m = Matrix4x4.Identity;
        model.Transform = Matrix4x4.CreateTranslation(1f, 0f, 0f);*/
        //观察空间
        var cameraPos = new Vector3(0, 0, -1f);
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
            var clipVertices = new ClipVertex[mesh.Vertices.Length];
            for (int j = 0; j < mesh.Vertices.Length; j++) {
                var vertex = mesh.Vertices[j];
                clipVertices[j] = new ClipVertex() {
                    Pos = Vector4.Transform(
                        new Vector4(vertex.X, vertex.Y, vertex.Z, 1f),
                        mvp),
                    Color = vertex.Color,
                };
            }
            for (int j = 0; j < mesh.Indices.Length; j+=3) {
                var clipV0 = clipVertices[mesh.Indices[j]];
                var clipV1 = clipVertices[mesh.Indices[j + 1]];
                var clipV2 = clipVertices[mesh.Indices[j + 2]];
                //近平面裁剪
                var clippedVertices = ClipTriangleAgainstNearPlane(
                    clipV0,
                    clipV1,
                    clipV2);
                if (clippedVertices.Count < 3) {
                    continue;
                }

                //扇形三角化
                for (int k = 1; k < clippedVertices.Count - 1; k++) {
                    var triangle = new Triangle() {
                        V0 = ToScreenVertex(clippedVertices[0], screenWidth, screenHeight),
                        V1 = ToScreenVertex(clippedVertices[k], screenWidth, screenHeight),
                        V2 = ToScreenVertex(clippedVertices[k+1], screenWidth, screenHeight),
                    };
                    Rasterizer.DrawTriangle(frameBuffer, depthBuffer, triangle);
                }
                
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

    private static Vertex ToScreenVertex(ClipVertex clipVertex, int screenWidth, int screenHeight) {
        var clipPos = clipVertex.Pos;
        var ndc = new Vector3(
            clipPos.X / clipPos.W,
            clipPos.Y / clipPos.W,
            clipPos.Z / clipPos.W);
        return new Vertex {
            X = (ndc.X + 1f) * 0.5f * screenWidth,
            Y = (1f - ndc.Y) * 0.5f * screenHeight,
            Z = ndc.Z,
            Color = clipVertex.Color,
        };
    }

    private static List<ClipVertex> ClipTriangleAgainstNearPlane(ClipVertex v0, ClipVertex v1, ClipVertex v2) {
        var input = new[] { v0, v1, v2 };
        var output = new List<ClipVertex>();
        for (int i = 0; i < input.Length; i++) {
            var current = input[i];
            var next = input[(i + 1) % input.Length];
            var currentInside = current.Pos.Z >= 0f;
            var nextInside = next.Pos.Z >= 0f;
            if (currentInside && nextInside) {
                output.Add(next);
            }
            else if (currentInside && !nextInside) {
                output.Add(IntersectNearPlane(current, next));
            } 
            else if (!currentInside && nextInside) {
                output.Add(IntersectNearPlane(current, next));
                output.Add(next);
            }
        }
        
        return output;
    }

    private static ClipVertex IntersectNearPlane(ClipVertex a, ClipVertex b) {
        var t = a.Pos.Z / (a.Pos.Z - b.Pos.Z);
        var clipVertex = new ClipVertex() {
            Pos = Vector4.Lerp(a.Pos, b.Pos, t),
            Color = new Color(
                (byte)(a.Color.R + (b.Color.R - a.Color.R) * t),
                (byte)(a.Color.G + (b.Color.G - a.Color.G) * t),
                (byte)(a.Color.B + (b.Color.B - a.Color.B) * t),
                (byte)(a.Color.A + (b.Color.A - a.Color.A) * t)),
        };
        return clipVertex;
    }
}
