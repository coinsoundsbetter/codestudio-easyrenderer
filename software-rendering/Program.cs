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
        Raylib.InitWindow(width, height, "Perspective UV Interpolation Test");
        Raylib.SetTargetFPS(60);

        var frameBuffer = new FrameBuffer(width, height);
        var depthBuffer = new DepthBuffer(width, height);

        // 近边距离相机约 2.5 个单位，远边约 20 个单位。
        // 这使同一张棋盘纹理在屏幕上形成明显梯形，用来观察当前的屏幕空间线性 UV 插值误差。
        var model = CreatePerspectiveUvTestModel();
        var texture = Texture.CreateCheckerboard(240, 12);
        var cameraPosition = new Vector3(0f, 0f, -3f);
        var cameraYaw = 0f;
        var cameraPitch = 0f;
        const float cameraMoveSpeed = 3f;
        const float cameraTurnSpeed = 1.5f;

        while (!Raylib.WindowShouldClose()) {
            var deltaTime = Raylib.GetFrameTime();
            UpdateCamera(
                ref cameraPosition,
                ref cameraYaw,
                ref cameraPitch,
                deltaTime,
                cameraMoveSpeed,
                cameraTurnSpeed);
            var cameraForward = GetCameraForward(cameraYaw, cameraPitch);

            Raylib.BeginDrawing();

            frameBuffer.Clear(Color.Black);
            depthBuffer.Clear(float.MaxValue);
            
            DrawModel(
                frameBuffer,
                depthBuffer,
                model,
                texture,
                width,
                height,
                cameraPosition,
                cameraForward);

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    var pixel = frameBuffer.GetPixel(x, y);
                    Raylib.DrawPixel(x, y, pixel);
                }
            }

            Raylib.DrawText(
                "W/S: forward/back   A/D: strafe   Q/E: up/down   Arrow keys: look",
                12,
                12,
                18,
                Color.Yellow);

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    private static void UpdateCamera(
        ref Vector3 position,
        ref float yaw,
        ref float pitch,
        float deltaTime,
        float moveSpeed,
        float turnSpeed) {
        if (Raylib.IsKeyDown(KeyboardKey.Left)) yaw -= turnSpeed * deltaTime;
        if (Raylib.IsKeyDown(KeyboardKey.Right)) yaw += turnSpeed * deltaTime;
        if (Raylib.IsKeyDown(KeyboardKey.Up)) pitch += turnSpeed * deltaTime;
        if (Raylib.IsKeyDown(KeyboardKey.Down)) pitch -= turnSpeed * deltaTime;
        pitch = Math.Clamp(pitch, -1.4f, 1.4f);

        var forward = GetCameraForward(yaw, pitch);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        var move = Vector3.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) move += forward;
        if (Raylib.IsKeyDown(KeyboardKey.S)) move -= forward;
        if (Raylib.IsKeyDown(KeyboardKey.D)) move += right;
        if (Raylib.IsKeyDown(KeyboardKey.A)) move -= right;
        if (Raylib.IsKeyDown(KeyboardKey.E)) move += Vector3.UnitY;
        if (Raylib.IsKeyDown(KeyboardKey.Q)) move -= Vector3.UnitY;

        if (move != Vector3.Zero) {
            position += Vector3.Normalize(move) * moveSpeed * deltaTime;
        }
    }

    // yaw = 0 时相机朝世界空间 +Z，与初始相机位置 (0, 0, -3) 对应。
    private static Vector3 GetCameraForward(float yaw, float pitch) {
        var cosPitch = MathF.Cos(pitch);
        return Vector3.Normalize(new Vector3(
            MathF.Sin(yaw) * cosPitch,
            MathF.Sin(pitch),
            MathF.Cos(yaw) * cosPitch));
    }

    // 一个由两个顺时针三角形组成的倾斜四边形。
    // 近边为 y = -1.0, z = -0.5；远边为 y = 1.3, z = 17。
    // UV 覆盖整张棋盘，让错误的仿射（屏幕空间线性）插值更容易看出。
    private static Model CreatePerspectiveUvTestModel() {
        var vertices = new[] {
            new Vertex { X = -1.2f, Y = -1.0f, Z = -0.5f, Color = Color.White, UV = new Vector2(0f, 0f) },
            new Vertex { X =  1.2f, Y = -1.0f, Z = -0.5f, Color = Color.White, UV = new Vector2(1f, 0f) },
            new Vertex { X =  1.2f, Y =  1.3f, Z = 17f, Color = Color.White, UV = new Vector2(1f, 1f) },
            new Vertex { X = -1.2f, Y =  1.3f, Z = 17f, Color = Color.White, UV = new Vector2(0f, 1f) },
        };

        return new Model {
            Meshes = new[] {
                new Mesh {
                    Vertices = vertices,
                    // 当前 LookAt 的相机左右方向会使模型 x 映射到屏幕时镜像；
                    // 此顺序投影后仍是项目约定的屏幕顺时针正面。
                    Indices = new[] { 0, 1, 2, 0, 2, 3 },
                },
            },
            Transform = Matrix4x4.Identity,
        };
    }

    private static void DrawModel(
        FrameBuffer frameBuffer,
        DepthBuffer depthBuffer,
        Model model,
        Texture texture,
        int screenWidth,
        int screenHeight,
        Vector3 cameraPos,
        Vector3 cameraForward) {
        //模型空间
        /*var m = Matrix4x4.Identity;
        model.Transform = Matrix4x4.CreateTranslation(1f, 0f, 0f);*/
        //观察空间
        var cameraTarget = cameraPos + cameraForward;
        var cameraUp = Vector3.UnitY;
        var view = Matrix4x4.CreateLookAt(cameraPos, cameraTarget, cameraUp);
        //透视投影
        var fov = MathF.PI / 4f; //45°
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
                    UV = vertex.UV,
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
                    Rasterizer.DrawTriangle(frameBuffer, depthBuffer, triangle, texture);
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

        /*var meshes = new Mesh[scene.MeshCount];

        for (int i = 0; i < scene.MeshCount; i++) {
            meshes[i] = ConvertToMesh(scene.Meshes[i]);
        }*/
        
        var meshes = new[] {
            ConvertToMesh(scene.Meshes[0]),
        };

        return new Model {
            Meshes = meshes,
            Transform = System.Numerics.Matrix4x4.Identity
        };
    }

    private static Mesh ConvertToMesh(Assimp.Mesh source) {
        var vertices = new Vertex[source.VertexCount];
        var hasUv0 = source.HasTextureCoords(0);
        Console.WriteLine(
            $"UV0: {hasUv0}, 顶点数: {source.VertexCount}, " +
            $"UV 数量: {source.TextureCoordinateChannels[0]?.Count ?? 0}");
        
        for (int i = 0; i < source.VertexCount; i++) {
            var position = source.Vertices[i];
            var uv = hasUv0
                ? new Vector2(
                    source.TextureCoordinateChannels[0][i].X,
                    source.TextureCoordinateChannels[0][i].Y)
                : Vector2.Zero;
            vertices[i] = new Vertex() {
                X = position.X,
                Y = position.Y,
                Z = position.Z,
                Color = Color.White,
                UV = uv,
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
            UV = clipVertex.UV,
            InvW = 1f / clipPos.W,
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
            UV = Vector2.Lerp(a.UV, b.UV, t),
            Pos = Vector4.Lerp(a.Pos, b.Pos, t),
            Color = new Color(
                (byte)(a.Color.R + (b.Color.R - a.Color.R) * t),
                (byte)(a.Color.G + (b.Color.G - a.Color.G) * t),
                (byte)(a.Color.B + (b.Color.B - a.Color.B) * t),
                (byte)(a.Color.A + (b.Color.A - a.Color.A) * t)),
        };
    }
}
