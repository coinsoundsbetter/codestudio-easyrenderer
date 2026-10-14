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
    private static void Main(string[] args) {
        if (args.Contains("--diagnose-input")) {
            InputDiagnostics.Run();
            return;
        }
        if (args.Contains("--verify-stage1")) {
            StageOneVerification.Run();
            return;
        }
        var exportIndex = Array.IndexOf(args, "--export-stage1");
        if (exportIndex >= 0) {
            var directory = args.Length > exportIndex + 1 ? args[exportIndex + 1]
                : Path.Combine(Environment.CurrentDirectory, "StudyOutputs");
            StageOneVerification.Export(directory);
            return;
        }

        var width = 800;
        var height = 600;
        Raylib.InitWindow(width, height, "Software Rendering");
        Raylib.SetTargetFPS(60);

        var frameBuffer = new FrameBuffer(width, height);
        var depthBuffer = new DepthBuffer(width, height);

        // 使用自带鸭子和配套 UV 纹理，便于观察曲面上的漫反射明暗。
        var duck = LoadModel(Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "duck.fbx"));
        var duckTexture = Texture.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "duckCM.png"));
        duckTexture.GenerateMipMaps();
        var plane = CreateLodTestPlane();
        var stripeTexture = Texture.CreateVerticalStripeTexture(512, 512, 16);
        stripeTexture.GenerateMipMaps();
        var whiteTexture = Texture.CreateVerticalStripeTexture(1, 1, 1);
        var light = new Lighting { NormalMap = Texture.CreateNormalMap() };
        var rotation = new RotationStudy();
        var showPlane = false;
        var showWhite = false;
        var nonuniformScale = false;
        var showHelp = true;
        var showRayStudy = false;
        var rayShadows = true;
        FrameBuffer? rayFrame = null;
        var lightAngle = -3f * MathF.PI / 4f; // 初始光源在上方、靠近相机的一侧。

        var modelZ = 0f;
        const float modelMoveSpeed = 1f;
        
        // 创建相机
        var cam = new Camera() {
            Position = new Vector3(0, 0, -3f),
            Forward = Vector3.UnitZ,
            Up = Vector3.UnitY,
            FieldOfView = MathF.PI / 4f, // 垂直 FOV：45°，使用弧度。
            NearPlane = 0.1f,
            FarPlane = 100f,
        };

        while (!Raylib.WindowShouldClose()) {

            // 调试暂停后不让一次过大的帧时间导致模型突然飞走。
            var delta = MathF.Min(Raylib.GetFrameTime(), 0.05f);
            // 左右键改变光源方向，L 键切换光照，便于比较纹理原色与受光结果。
            if (Raylib.IsKeyDown(KeyboardKey.Left)) lightAngle -= delta;
            if (Raylib.IsKeyDown(KeyboardKey.Right)) lightAngle += delta;
            light.DirectionToLight = new Vector3(0f, -MathF.Cos(lightAngle), MathF.Sin(lightAngle));
            if (Raylib.IsKeyPressed(KeyboardKey.L)) light.Enabled = !light.Enabled;
            if (Raylib.IsKeyPressed(KeyboardKey.One)) light.Model = ShadingModel.Lambert;
            if (Raylib.IsKeyPressed(KeyboardKey.Two)) light.Model = ShadingModel.Phong;
            if (Raylib.IsKeyPressed(KeyboardKey.Three)) light.Model = ShadingModel.BlinnPhong;
            if (Raylib.IsKeyPressed(KeyboardKey.N)) light.UseNormalMap = !light.UseNormalMap;
            if (Raylib.IsKeyPressed(KeyboardKey.F)) light.UseAnisotropic = !light.UseAnisotropic;
            if (Raylib.IsKeyPressed(KeyboardKey.M)) light.UseCorrectNormalMatrix = !light.UseCorrectNormalMatrix;
            if (Raylib.IsKeyPressed(KeyboardKey.B)) nonuniformScale = !nonuniformScale;
            if (Raylib.IsKeyPressed(KeyboardKey.U)) showWhite = !showWhite;
            if (Raylib.IsKeyPressed(KeyboardKey.H)) showHelp = !showHelp;
            // 光追对照是独立球体场景，低分辨率计算一次后复用，不影响鸭子的管线。
            if (Raylib.IsKeyPressed(KeyboardKey.K)) {
                showRayStudy = !showRayStudy;
                if (showRayStudy) rayFrame ??= RayTracingStudy.Render(320, 240, rayShadows);
            }
            if (showRayStudy && Raylib.IsKeyPressed(KeyboardKey.J)) {
                rayShadows = !rayShadows;
                rayFrame = RayTracingStudy.Render(320, 240, rayShadows);
            }
            if (Raylib.IsKeyPressed(KeyboardKey.P)) {
                showPlane = !showPlane;
                modelZ = 0f;
                rotation = new RotationStudy();
                // 条纹平面用于过滤对比，切回鸭子时恢复光照。
                light.Enabled = !showPlane;
            }
            if (Raylib.IsKeyPressed(KeyboardKey.T))
                rotation.SetMode((RotationMode)(((int)rotation.Mode + 1) % 3));
            if (Raylib.IsKeyPressed(KeyboardKey.G)) rotation.SetGimbalPose();
            if (Raylib.IsKeyPressed(KeyboardKey.R)) rotation.BeginReset();
            if (Raylib.IsKeyDown(KeyboardKey.S)) {
                modelZ -= modelMoveSpeed * delta;
            }
            if (Raylib.IsKeyDown(KeyboardKey.W)) {
                modelZ += modelMoveSpeed * delta;
            }

            float Axis(KeyboardKey negative, KeyboardKey positive) =>
                (Raylib.IsKeyDown(positive) ? delta : 0f) - (Raylib.IsKeyDown(negative) ? delta : 0f);
            rotation.Update(delta, Axis(KeyboardKey.A, KeyboardKey.D),
                Axis(KeyboardKey.Q, KeyboardKey.E), Axis(KeyboardKey.Z, KeyboardKey.C));
            var model = showPlane ? plane : duck;
            var texture = showWhite ? whiteTexture : (showPlane ? stripeTexture : duckTexture);
            // 鸭子原始尺寸较大：先缩小，再旋转，最后放到相机前方。
            var scale = showPlane ? 1f : 0.01f;
            var scaleAxes = nonuniformScale ? new Vector3(1.7f, 0.6f, 1f) : Vector3.One;
            model.Transform = Matrix4x4.CreateScale(scaleAxes * scale)
                              * rotation.Matrix
                              * Matrix4x4.CreateTranslation(0f, showPlane ? 0f : -0.5f, modelZ);
            light.CameraPosition = cam.Position;
            
            Raylib.BeginDrawing();

            frameBuffer.Clear(Color.Black);
            depthBuffer.Clear(float.MaxValue);
            
            if (!showRayStudy)
                DrawModel(cam, frameBuffer, depthBuffer, model, texture, width, height, light);

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    var pixel = showRayStudy && rayFrame is not null
                        ? rayFrame.GetPixel(x * rayFrame.Width / width, y * rayFrame.Height / height)
                        : frameBuffer.GetPixel(x, y);
                    Raylib.DrawPixel(x, y, pixel);
                }
            }

            var status = showRayStudy ? $"Ray tracing study | shadows: {rayShadows} (cached image)"
                : $"{light.Model} | light {(light.Enabled ? "on" : "off")} | {rotation.Mode} | {Raylib.GetFPS()} FPS";
            Raylib.DrawText(status, 10, 10, 16, Color.Green);
            if (showHelp) {
                Raylib.DrawText("1 Lambert / 2 Phong / 3 Blinn-Phong | L light | Left/Right light direction", 10, 32, 14, Color.White);
                Raylib.DrawText($"N normal map: {light.UseNormalMap} | B stretch: {nonuniformScale} | M inverse transpose: {light.UseCorrectNormalMatrix}", 10, 52, 14, Color.White);
                Raylib.DrawText($"P duck/stripe plane | F anisotropic: {light.UseAnisotropic} | U white texture | H help", 10, 72, 14, Color.White);
                Raylib.DrawText("W/S move | A/D yaw | Q/E pitch | Z/C roll | T rotation mode | G gimbal pose | R slerp reset", 10, 92, 14, Color.White);
                Raylib.DrawText("K raster/ray tracing study | J ray shadows | ray scene ignores model/light controls", 10, 112, 14, Color.White);
                if (rotation.Mode == RotationMode.Euler && !showRayStudy)
                    Raylib.DrawText($"Euler degrees: yaw {rotation.Yaw * 180f / MathF.PI:F0}, pitch {rotation.Pitch * 180f / MathF.PI:F0}, roll {rotation.Roll * 180f / MathF.PI:F0}", 10, 132, 14, Color.Green);
            }
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    internal static Model CreateLodTestPlane() {
        // 近边 z=0，远边 z=18；UV 的 U 在横向重复 4 次。
        // 索引顺序遵循本项目“屏幕顺时针为正面”的约定。
        var vertices = new[] {
            new Vertex { X = -1.5f, Y = -1.0f, Z = 0f,  Color = Color.White, UV = new Vector2(0f, 0f) },
            new Vertex { X =  1.5f, Y = -1.0f, Z = 0f,  Color = Color.White, UV = new Vector2(4f, 0f) },
            new Vertex { X =  1.5f, Y =  1.0f, Z = 18f, Color = Color.White, UV = new Vector2(4f, 1f) },
            new Vertex { X = -1.5f, Y =  1.0f, Z = 18f, Color = Color.White, UV = new Vector2(0f, 1f) },
        };

        // 用同一起点的两条边求法线；该顺序得到朝向当前可见面的法线。
        var a = new Vector3(vertices[0].X, vertices[0].Y, vertices[0].Z);
        var b = new Vector3(vertices[1].X, vertices[1].Y, vertices[1].Z);
        var c = new Vector3(vertices[2].X, vertices[2].Y, vertices[2].Z);
        var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        for (var i = 0; i < vertices.Length; i++) {
            vertices[i].Normal = normal;
            // U 沿边 AB；V 沿边 BC，B = cross(N,T) 与 +V 同向。
            vertices[i].Tangent = new Vector4(Vector3.UnitX, 1f);
        }

        return new Model {
            Meshes = new[] {
                new Mesh {
                    Vertices = vertices,
                    Indices = new[] { 0, 1, 2, 0, 2, 3 },
                },
            },
            Transform = Matrix4x4.Identity,
        };
    }

    internal static void DrawModel(Camera mainCamera, FrameBuffer frameBuffer, DepthBuffer depthBuffer, Model model, Texture texture, int screenWidth, int screenHeight, Lighting light) {
        //观察空间
        var view = mainCamera.GetViewMatrix();
        //透视投影
        var aspectRatio = (float)screenWidth / screenHeight;
        var projection = mainCamera.GetProjectionMatrix(aspectRatio);
        //组合矩阵
        var mvp = model.Transform * view * projection;
        // 光照在世界空间计算。法线不能用 MVP 变换，也不能包含平移。
        // 逆转置让非均匀缩放后的法线仍垂直于表面。
        if (!Matrix4x4.Invert(model.Transform, out var inverseModel)) {
            throw new InvalidOperationException("模型变换不可逆，无法计算法线矩阵。");
        }
        // M 键故意使用错误算法，配合 B 非均匀缩放观察差别。
        var normalMatrix = light.UseCorrectNormalMatrix ? Matrix4x4.Transpose(inverseModel) : model.Transform;
        var modelHandedness = model.Transform.GetDeterminant() < 0f ? -1f : 1f;
        for (int i = 0; i < model.Meshes.Length; i++) {
            var mesh = model.Meshes[i];
            var clipVertices = new ClipVertex[mesh.Vertices.Length];
            for (int j = 0; j < mesh.Vertices.Length; j++) {
                var vertex = mesh.Vertices[j];
                if (i == 0 && j == 0) {
                    // 学习观察：只跟踪第一个网格的第一个顶点。
                    var viewPos = Vector4.Transform(
                        new Vector4(vertex.X, vertex.Y, vertex.Z, 1f),
                        model.Transform * view);
                    var clipPos = Vector4.Transform(viewPos, projection);
                    var ndcX = clipPos.X / clipPos.W;
                    // 在下一行设置断点，此时上面的三个变量均已计算完成。
                    GC.KeepAlive(ndcX);
                }
                clipVertices[j] = new ClipVertex() {
                    Pos = Vector4.Transform(
                        new Vector4(vertex.X, vertex.Y, vertex.Z, 1f),
                        mvp),
                    Color = vertex.Color,
                    UV = vertex.UV,
                    Normal = Lighting.NormalizeOr(Vector3.TransformNormal(vertex.Normal, normalMatrix), Vector3.UnitY),
                    WorldPosition = Vector3.Transform(new Vector3(vertex.X, vertex.Y, vertex.Z), model.Transform),
                    Tangent = new Vector4(Vector3.TransformNormal(
                        new Vector3(vertex.Tangent.X, vertex.Tangent.Y, vertex.Tangent.Z), model.Transform),
                        vertex.Tangent.W * modelHandedness),
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
                    Rasterizer.DrawTriangle(frameBuffer, depthBuffer, triangle, texture, light);
                }
                
            }
        }
    }

    internal static Model LoadModel(string path) {
        if (!File.Exists(path)) {
            throw new FileNotFoundException("模型文件不存在。", path);
        }

        using var importer = new AssimpContext();

        var scene = importer.ImportFile(
            path,
            PostProcessSteps.Triangulate |
            PostProcessSteps.PreTransformVertices |
            // 导入模型外表面默认绕序与当前“屏幕顺时针为正面”相反。
            // 只翻转索引顺序，保留模型的外向法线和 UV。
            PostProcessSteps.FlipWindingOrder |
            PostProcessSteps.GenerateSmoothNormals |
            PostProcessSteps.CalculateTangentSpace
        );

        if (scene is null || scene.MeshCount == 0) {
            throw new InvalidDataException("模型文件中没有可绘制的网格。");
        }
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
            var normal = source.HasNormals
                ? new Vector3(source.Normals[i].X, source.Normals[i].Y, source.Normals[i].Z)
                : Vector3.Zero;
            var tangent = Vector4.Zero;
            if (source.HasTangentBasis) {
                var t = new Vector3(source.Tangents[i].X, source.Tangents[i].Y, source.Tangents[i].Z);
                var b = new Vector3(source.BiTangents[i].X, source.BiTangents[i].Y, source.BiTangents[i].Z);
                var sign = Vector3.Dot(Vector3.Cross(normal, t), b) < 0f ? -1f : 1f;
                tangent = new Vector4(t, sign);
            }
            vertices[i] = new Vertex() {
                X = position.X,
                Y = position.Y,
                Z = position.Z,
                Color = Color.White,
                UV = uv,
                Normal = normal,
                Tangent = tangent,
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
            Normal = clipVertex.Normal,
            WorldPosition = clipVertex.WorldPosition,
            Tangent = clipVertex.Tangent,
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
            Normal = Vector3.Lerp(a.Normal, b.Normal, t),
            WorldPosition = Vector3.Lerp(a.WorldPosition, b.WorldPosition, t),
            Tangent = Vector4.Lerp(a.Tangent, b.Tangent, t),
            Color = new Color(
                (byte)(a.Color.R + (b.Color.R - a.Color.R) * t),
                (byte)(a.Color.G + (b.Color.G - a.Color.G) * t),
                (byte)(a.Color.B + (b.Color.B - a.Color.B) * t),
                (byte)(a.Color.A + (b.Color.A - a.Color.A) * t)),
        };
    }
}
