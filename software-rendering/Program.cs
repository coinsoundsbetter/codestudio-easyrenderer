using System.Numerics;
using Assimp;
using Raylib_cs;

namespace SoftwareRendering;

internal static class Program
{
    private static readonly ClipPlane[] FrustumPlanes = [
        ClipPlane.Left,
        ClipPlane.Right,
        ClipPlane.Bottom,
        ClipPlane.Top,
        ClipPlane.Near,
        ClipPlane.Far,
    ];

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
                // 齐次裁剪空间内的完整视锥裁剪。
                // 必须在透视除法之前完成，因为所有平面都依赖 w。
                var clippedVertices = ClipTriangleAgainstFrustum(
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

    private static List<ClipVertex> ClipTriangleAgainstFrustum(ClipVertex v0, ClipVertex v1, ClipVertex v2) {
        var polygon = new List<ClipVertex> { v0, v1, v2 };

        foreach (var plane in FrustumPlanes) {
            polygon = ClipPolygonAgainstPlane(polygon, plane);
            if (polygon.Count == 0) {
                break;
            }
        }

        return polygon;
    }

    // Sutherland-Hodgman：将一个凸多边形裁剪到单个齐次平面内。
    private static List<ClipVertex> ClipPolygonAgainstPlane(
        IReadOnlyList<ClipVertex> input,
        ClipPlane plane) {
        var output = new List<ClipVertex>();

        for (int i = 0; i < input.Count; i++) {
            var current = input[i];
            var next = input[(i + 1) % input.Count];
            var currentDistance = GetPlaneDistance(current.Pos, plane);
            var nextDistance = GetPlaneDistance(next.Pos, plane);
            var currentInside = currentDistance >= 0f;
            var nextInside = nextDistance >= 0f;

            if (currentInside && nextInside) {
                output.Add(next);
            }
            else if (currentInside && !nextInside) {
                output.Add(Intersect(current, next, currentDistance, nextDistance));
            } 
            else if (!currentInside && nextInside) {
                output.Add(Intersect(current, next, currentDistance, nextDistance));
                output.Add(next);
            }
        }

        return output;
    }

    // 平面内侧统一表示为 distance >= 0。
    private static float GetPlaneDistance(Vector4 position, ClipPlane plane) {
        return plane switch {
            ClipPlane.Left => position.X + position.W, // x >= -w
            ClipPlane.Right => position.W - position.X, // x <=  w
            ClipPlane.Bottom => position.Y + position.W, // y >= -w
            ClipPlane.Top => position.W - position.Y, // y <=  w
            ClipPlane.Near => position.Z, // z >= 0
            ClipPlane.Far => position.W - position.Z, // z <= w
            _ => throw new ArgumentOutOfRangeException(nameof(plane)),
        };
    }

    private static ClipVertex Intersect(
        ClipVertex a,
        ClipVertex b,
        float distanceA,
        float distanceB) {
        // A + t(B - A) 落到平面上时，distance = 0。
        var t = distanceA / (distanceA - distanceB);

        return new ClipVertex {
            Pos = Vector4.Lerp(a.Pos, b.Pos, t),
            Color = new Color(
                (byte)(a.Color.R + (b.Color.R - a.Color.R) * t),
                (byte)(a.Color.G + (b.Color.G - a.Color.G) * t),
                (byte)(a.Color.B + (b.Color.B - a.Color.B) * t),
                (byte)(a.Color.A + (b.Color.A - a.Color.A) * t)),
        };
    }
}
