using System.Numerics;
using System.Text;
using Raylib_cs;

namespace SoftwareRendering;

// 无窗口实验与回归检查：dotnet run -- --verify-stage1 / --export-stage1 路径。
internal static class StageOneVerification {
    private static Camera CreateCamera() => new() {
        Position = new Vector3(0, 0, -3), Forward = Vector3.UnitZ, Up = Vector3.UnitY,
        FieldOfView = MathF.PI / 4f, NearPlane = 0.1f, FarPlane = 100f
    };

    private static void Check(bool success, string message) {
        if (!success) throw new InvalidOperationException("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
    private static bool Close(Vector3 a, Vector3 b, float epsilon = 1e-4f) => Vector3.Distance(a, b) < epsilon;
    private static float Difference(Matrix4x4 a, Matrix4x4 b) =>
        Vector3.Distance(Vector3.TransformNormal(Vector3.UnitX, a), Vector3.TransformNormal(Vector3.UnitX, b)) +
        Vector3.Distance(Vector3.TransformNormal(Vector3.UnitY, a), Vector3.TransformNormal(Vector3.UnitY, b)) +
        Vector3.Distance(Vector3.TransformNormal(Vector3.UnitZ, a), Vector3.TransformNormal(Vector3.UnitZ, b));

    public static void Run() {
        var camera = CreateCamera();
        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix(4f / 3f);
        var origin = Vector4.Transform(new Vector4(0, 0, 0, 1), view);
        var clip = Vector4.Transform(origin, projection);
        Check(MathF.Abs(origin.Z + 3f) < 1e-5f && MathF.Abs(clip.W - 3f) < 1e-5f, "View front=-Z and clip.W=-view.Z");
        var near = Vector4.Transform(new Vector4(1, 0, -3, 1), projection);
        var far = Vector4.Transform(new Vector4(1, 0, -6, 1), projection);
        Check(MathF.Abs(near.X / near.W - 2f * far.X / far.W) < 1e-5f, "doubling depth halves projected X");
        var nearPlane = Vector4.Transform(new Vector4(0, 0, -camera.NearPlane, 1), projection);
        var farPlane = Vector4.Transform(new Vector4(0, 0, -camera.FarPlane, 1), projection);
        Check(MathF.Abs(nearPlane.Z / nearPlane.W) < 1e-5f && MathF.Abs(farPlane.Z / farPlane.W - 1f) < 1e-5f,
            "near/far map to NDC depth 0/1");

        var edge1 = new Vector3(1, 1, 0);
        var edge2 = new Vector3(0, 1, 1);
        var n = Vector3.Normalize(Vector3.Cross(edge1, edge2));
        var model = Matrix4x4.CreateScale(2f, 0.5f, 1.3f) * Matrix4x4.CreateRotationY(0.4f);
        Matrix4x4.Invert(model, out var inverse);
        var correct = Vector3.TransformNormal(n, Matrix4x4.Transpose(inverse));
        var wrong = Vector3.TransformNormal(n, model);
        var transformedEdge = Vector3.TransformNormal(edge1, model);
        Check(MathF.Abs(Vector3.Dot(correct, transformedEdge)) < 1e-5f && MathF.Abs(Vector3.Dot(wrong, transformedEdge)) > 0.1f,
            "inverse transpose preserves perpendicularity; ordinary transform does not");
        var translated = model * Matrix4x4.CreateTranslation(8, -5, 4);
        Matrix4x4.Invert(translated, out var translatedInverse);
        Check(Close(correct, Vector3.TransformNormal(n, Matrix4x4.Transpose(translatedInverse))), "translation does not change normals");

        var light = new Lighting { DirectionToLight = Vector3.UnitZ, CameraPosition = new Vector3(0, 0, 3),
            Ambient = 0, DiffuseStrength = 0.5f, SpecularStrength = 0.25f };
        var baseColor = new Color(100, 80, 60, 123);
        var lambert = light.Shade(baseColor, Vector3.UnitZ, Vector3.Zero);
        light.Model = ShadingModel.Phong;
        var phong = light.Shade(baseColor, Vector3.UnitZ, Vector3.Zero);
        light.Model = ShadingModel.BlinnPhong;
        var blinn = light.Shade(baseColor, Vector3.UnitZ, Vector3.Zero);
        Check(lambert.R == 50 && phong.R > lambert.R && blinn.R > lambert.R && blinn.A == 123, "Lambert + Phong/Blinn highlight and preserved alpha");
        Check(light.Shade(baseColor, -Vector3.UnitZ, Vector3.Zero).R == 0, "backlit surface has no diffuse or specular");
        var frontHighlight = light.Shade(baseColor, Vector3.UnitZ, Vector3.Zero).R;
        light.CameraPosition = new Vector3(3, 0, 0);
        Check(light.Shade(baseColor, Vector3.UnitZ, Vector3.Zero).R < frontHighlight, "specular depends on view direction");
        light.Enabled = false;
        Check(light.Shade(baseColor, n, Vector3.Zero).R == baseColor.R, "light off returns albedo");

        light.NormalMap = Texture.CreateNormalMap(); light.UseNormalMap = true; light.NormalMapTiling = 1;
        var tangent = new Vector4(1, 0, 0, 1);
        var mapped = light.ApplyNormalMap(Vector3.UnitZ, tangent, new Vector2(.13f, .27f), Vector2.Zero, Vector2.Zero);
        var rotation = Matrix4x4.CreateRotationY(.7f);
        var rotatedTangent = new Vector4(Vector3.TransformNormal(Vector3.UnitX, rotation), 1);
        var rotatedMapped = light.ApplyNormalMap(Vector3.TransformNormal(Vector3.UnitZ, rotation), rotatedTangent,
            new Vector2(.13f, .27f), Vector2.Zero, Vector2.Zero);
        Check(MathF.Abs(mapped.Length() - 1f) < 1e-5f && Close(rotatedMapped, Vector3.TransformNormal(mapped, rotation)), "TBN normal mapping rotates with geometry and stays unit length");
        var mirrored = light.ApplyNormalMap(Vector3.UnitZ, new Vector4(1, 0, 0, -1), new Vector2(.13f, .27f), Vector2.Zero, Vector2.Zero);
        Check(MathF.Abs(mapped.X - mirrored.X) < 1e-5f && MathF.Abs(mapped.Y + mirrored.Y) < 1e-5f, "mirrored UV flips bitangent sign");

        var stripes = Texture.CreateVerticalStripeTexture(256, 256, 8); stripes.GenerateMipMaps();
        var uv = new Vector2(4.5f / 256f, .3f);
        var dx = new Vector2(1f / 256f, 0); var dy = new Vector2(0, 64f / 256f);
        var isotropic = stripes.SampleFiltered(uv, dx, dy, false);
        var anisotropic = stripes.SampleFiltered(uv, dx, dy, true, 16);
        Check(anisotropic.R > isotropic.R + 60, "anisotropic footprint retains narrow-axis stripe detail");
        var boundaryLeft = stripes.SampleBilinear(new Vector2(-1e-5f, .3f));
        var boundaryRight = stripes.SampleBilinear(new Vector2(1e-5f, .3f));
        Check(Math.Abs(boundaryLeft.R - boundaryRight.R) <= 2, "Repeat bilinear filtering is continuous at UV seam");

        var euler = RotationStudy.EulerMatrix(.4f, .3f, -.2f);
        var quaternionMatrix = Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromYawPitchRoll(.4f, .3f, -.2f));
        Check(Difference(euler, quaternionMatrix) < 1e-4f, "Euler Z-X-Y agrees with quaternion for same angles");
        Check(Difference(RotationStudy.EulerMatrix(.2f, MathF.PI / 2f, 0),
            RotationStudy.EulerMatrix(0, MathF.PI / 2f, -.2f)) < 1e-4f, "at pitch 90 yaw and roll lose independent axes (gimbal lock)");
        var local = new RotationStudy { Mode = RotationMode.QuaternionLocal,
            Orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, .7f) };
        var world = new RotationStudy { Mode = RotationMode.QuaternionWorld, Orientation = local.Orientation };
        var start = local.Matrix; var deltaRotation = Matrix4x4.CreateRotationY(.3f);
        local.Update(0, .3f, 0, 0); world.Update(0, .3f, 0, 0);
        Check(Difference(local.Matrix, deltaRotation * start) < 1e-4f && Difference(world.Matrix, start * deltaRotation) < 1e-4f,
            "quaternion local/world composition matches row-vector matrices");
        local.BeginReset(); local.Update(.5f, 0, 0, 0);
        Check(local.IsResetting && MathF.Abs(local.Orientation.Length() - 1f) < 1e-5f, "Slerp midpoint is a unit quaternion");
        local.Update(.5f, 0, 0, 0);
        Check(Difference(local.Matrix, Matrix4x4.Identity) < 1e-5f, "Slerp reset reaches identity");

        VerifyDepthAndCulling();
        VerifyModelAndClipping();
        var traced = RayTracingStudy.Render(80, 60, true);
        var unshadowed = RayTracingStudy.Render(80, 60, false);
        var changed = 0;
        for (int y = 0; y < 60; y++) for (int x = 0; x < 80; x++)
            if (traced.GetPixel(x, y).R < unshadowed.GetPixel(x, y).R) changed++;
        Check(changed > 10, "ray-traced shadow rays darken occluded surfaces");
        Console.WriteLine("All stage-one checks passed.");
    }

    private static void VerifyDepthAndCulling() {
        var fb = new FrameBuffer(16, 16); var db = new DepthBuffer(16, 16);
        var texture = Texture.CreateVerticalStripeTexture(1, 1, 1);
        Triangle Tri(float z) => new() {
            V0 = new Vertex { X = 1, Y = 1, Z = z, InvW = 1 },
            V1 = new Vertex { X = 14, Y = 1, Z = z, InvW = 1 },
            V2 = new Vertex { X = 1, Y = 14, Z = z, InvW = 1 }
        };
        foreach (var order in new[] { new[] { .2f, .8f }, new[] { .8f, .2f } }) {
            fb.Clear(Color.Black); db.Clear(float.MaxValue);
            foreach (var depth in order) Rasterizer.DrawTriangle(fb, db, Tri(depth), texture);
            Check(MathF.Abs(db.GetDepth(3, 3) - .2f) < 1e-5f, "depth keeps nearest triangle in either draw order");
        }
        var reversed = Tri(.2f); (reversed.V1, reversed.V2) = (reversed.V2, reversed.V1);
        db.Clear(float.MaxValue); Rasterizer.DrawTriangle(fb, db, reversed, texture);
        Check(db.GetDepth(3, 3) == float.MaxValue, "counterclockwise screen triangle is culled");
    }

    private static void VerifyModelAndClipping() {
        var duck = Program.LoadModel(Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "duck.fbx"));
        var allVertices = duck.Meshes.SelectMany(mesh => mesh.Vertices).ToArray();
        Check(allVertices.Length > 0 && allVertices.Count(v => v.Normal.LengthSquared() > .1f) > allVertices.Length * .9f,
            "duck imports normals");
        Check(allVertices.Count(v => new Vector3(v.Tangent.X, v.Tangent.Y, v.Tangent.Z).LengthSquared() > .1f) > allVertices.Length * .9f,
            "duck imports tangent basis");
        var light = new Lighting { CameraPosition = CreateCamera().Position, Model = ShadingModel.BlinnPhong,
            NormalMap = Texture.CreateNormalMap(), UseNormalMap = true };
        var fb = new FrameBuffer(160, 120); var db = new DepthBuffer(160, 120);
        var texture = Texture.CreateVerticalStripeTexture(1, 1, 1);
        duck.Transform = Matrix4x4.CreateScale(.01f) * Matrix4x4.CreateTranslation(0, -.5f, 0);
        fb.Clear(Color.Black); db.Clear(float.MaxValue);
        Program.DrawModel(CreateCamera(), fb, db, duck, texture, 160, 120, light);
        Check(CountCovered(db, 160, 120) > 100, "textured/shaded duck renders through full pipeline");
        var plane = Program.CreateLodTestPlane();
        var planeVertex = plane.Meshes[0].Vertices[0];
        var planeTangent = new Vector3(planeVertex.Tangent.X, planeVertex.Tangent.Y, planeVertex.Tangent.Z);
        Check(Close(Vector3.Cross(planeVertex.Normal, planeTangent) * planeVertex.Tangent.W,
            Vector3.Normalize(new Vector3(0, 2, 18))), "procedural plane TBN agrees with positive V direction");
        plane.Transform = Matrix4x4.CreateTranslation(0, 0, -3.5f);
        fb.Clear(Color.Black); db.Clear(float.MaxValue);
        Program.DrawModel(CreateCamera(), fb, db, plane, texture, 160, 120, light);
        Check(CountCovered(db, 160, 120) > 100, "clipping across near plane retains shaded visible portion");
        plane.Transform = Matrix4x4.CreateTranslation(0, 0, -30f);
        db.Clear(float.MaxValue);
        Program.DrawModel(CreateCamera(), fb, db, plane, texture, 160, 120, light);
        Check(CountCovered(db, 160, 120) == 0, "geometry fully behind camera produces no pixels");
    }

    private static int CountCovered(DepthBuffer db, int width, int height) {
        var covered = 0;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
            var depth = db.GetDepth(x, y);
            if (depth == float.MaxValue) continue;
            if (!float.IsFinite(depth) || depth < -1e-5f || depth > 1.00001f)
                throw new InvalidOperationException("Invalid rasterized depth");
            covered++;
        }
        return covered;
    }

    internal static void SavePpm(FrameBuffer frame, string path) {
        using var stream = File.Create(path);
        var header = Encoding.ASCII.GetBytes($"P6\n{frame.Width} {frame.Height}\n255\n");
        stream.Write(header);
        for (int y = 0; y < frame.Height; y++) for (int x = 0; x < frame.Width; x++) {
            var c = frame.GetPixel(x, y); stream.WriteByte(c.R); stream.WriteByte(c.G); stream.WriteByte(c.B);
        }
    }

    public static void Export(string directory) {
        Directory.CreateDirectory(directory);
        var camera = CreateCamera();
        var duck = Program.LoadModel(Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "duck.fbx"));
        var texture = Texture.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "duckCM.png"));
        texture.GenerateMipMaps();
        var light = new Lighting { CameraPosition = camera.Position, NormalMap = Texture.CreateNormalMap() };
        var fb = new FrameBuffer(320, 240); var db = new DepthBuffer(320, 240);
        var white = Texture.CreateVerticalStripeTexture(1, 1, 1);
        void Draw(string name, Model model, Texture albedo) {
            fb.Clear(Color.Black); db.Clear(float.MaxValue);
            Program.DrawModel(camera, fb, db, model, albedo, 320, 240, light);
            if (CountCovered(db, 320, 240) == 0) throw new InvalidOperationException("Empty export: " + name);
            SavePpm(fb, Path.Combine(directory, name + ".ppm"));
            Console.WriteLine("Exported " + name);
        }
        duck.Transform = Matrix4x4.CreateScale(.01f) * Matrix4x4.CreateTranslation(0, -.5f, 0);
        light.Enabled = false; Draw("01-unlit", duck, texture); light.Enabled = true;
        light.Model = ShadingModel.Lambert; Draw("02-lambert", duck, texture);
        light.Model = ShadingModel.Phong; Draw("03-phong", duck, texture);
        light.Model = ShadingModel.BlinnPhong; Draw("04-blinn-phong", duck, texture);
        light.UseNormalMap = true; Draw("05-normal-map", duck, texture); light.UseNormalMap = false;
        duck.Transform = Matrix4x4.CreateScale(.013f, .006f, .01f) * Matrix4x4.CreateRotationY(.4f)
            * Matrix4x4.CreateTranslation(0, -.3f, 0);
        Draw("06-correct-normals", duck, white);
        light.UseCorrectNormalMatrix = false; Draw("07-wrong-normals", duck, white); light.UseCorrectNormalMatrix = true;
        var plane = Program.CreateLodTestPlane();
        var stripes = Texture.CreateVerticalStripeTexture(512, 512, 16); stripes.GenerateMipMaps();
        light.Enabled = false; Draw("08-trilinear", plane, stripes);
        light.UseAnisotropic = true; Draw("09-anisotropic", plane, stripes);
        light.Enabled = true; light.UseAnisotropic = false;
        duck.Transform = Matrix4x4.CreateScale(.01f) * RotationStudy.EulerMatrix(0, MathF.PI / 2, 0)
            * Matrix4x4.CreateTranslation(0, -.2f, 0);
        Draw("10-gimbal-pose", duck, texture);
        var reset = new RotationStudy { Mode = RotationMode.QuaternionLocal,
            Orientation = Quaternion.CreateFromYawPitchRoll(.8f, .5f, -.4f) };
        reset.BeginReset(); reset.Update(.5f, 0, 0, 0);
        duck.Transform = Matrix4x4.CreateScale(.01f) * reset.Matrix * Matrix4x4.CreateTranslation(0, -.5f, 0);
        Draw("11-slerp-midpoint", duck, texture);
        SavePpm(RayTracingStudy.Render(320, 240, false), Path.Combine(directory, "12-raytrace-no-shadow.ppm"));
        SavePpm(RayTracingStudy.Render(320, 240, true), Path.Combine(directory, "13-raytrace-shadow.ppm"));
        Console.WriteLine("Export complete: " + Path.GetFullPath(directory));
    }
}
