using System.Numerics;
using Raylib_cs;

namespace SoftwareRendering;

// 阶段一的最小光追对照：相机射线找最近交点，阴影射线判断光是否被挡住。
// 它使用球体与地面，不读取鸭子网格，不包含反射、路径追踪或加速结构。
public static class RayTracingStudy {
    private readonly record struct Sphere(Vector3 Center, float Radius, Color Albedo);
    private readonly record struct Hit(float Distance, Vector3 Position, Vector3 Normal, Color Albedo);
    private static readonly Sphere[] Spheres = [
        new(new Vector3(-0.65f, 0f, 0.5f), 0.65f, new Color(220, 150, 40, 255)),
        new(new Vector3(0.65f, -0.2f, 1f), 0.45f, new Color(65, 145, 225, 255))
    ];

    // ray(t) = origin + t * direction。direction 必须是单位向量。
    private static float IntersectSphere(Vector3 origin, Vector3 direction, Sphere sphere) {
        var oc = origin - sphere.Center;
        var halfB = Vector3.Dot(oc, direction);
        var c = oc.LengthSquared() - sphere.Radius * sphere.Radius;
        var discriminant = halfB * halfB - c;
        if (discriminant < 0f) return float.PositiveInfinity;
        var root = MathF.Sqrt(discriminant);
        var near = -halfB - root;
        var far = -halfB + root;
        return near > 1e-4f ? near : (far > 1e-4f ? far : float.PositiveInfinity);
    }

    private static bool Trace(Vector3 origin, Vector3 direction, out Hit hit) {
        var nearest = float.PositiveInfinity;
        hit = default;
        foreach (var sphere in Spheres) {
            var distance = IntersectSphere(origin, direction, sphere);
            if (distance >= nearest) continue;
            nearest = distance;
            var point = origin + direction * distance;
            hit = new Hit(distance, point, Vector3.Normalize(point - sphere.Center), sphere.Albedo);
        }
        // 地面 y = -0.65。比较 t 就是在光追中解决可见性。
        if (MathF.Abs(direction.Y) > 1e-6f) {
            var distance = (-0.65f - origin.Y) / direction.Y;
            if (distance > 1e-4f && distance < nearest) {
                var point = origin + direction * distance;
                var checker = ((int)MathF.Floor(point.X * 2f) + (int)MathF.Floor(point.Z * 2f)) % 2 == 0;
                hit = new Hit(distance, point, Vector3.UnitY,
                    checker ? new Color(185, 185, 185, 255) : new Color(95, 95, 95, 255));
                nearest = distance;
            }
        }
        return float.IsFinite(nearest);
    }

    public static FrameBuffer Render(int width, int height, bool shadows = true) {
        var result = new FrameBuffer(width, height);
        var camera = new Vector3(0f, 0.45f, -3f);
        var forward = Vector3.Normalize(new Vector3(0f, -0.15f, 1f));
        // 与当前 Camera 相同：观察空间前方为 -Z，世界中朝 +Z 看。
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Cross(right, forward);
        var halfHeight = MathF.Tan(MathF.PI / 8f);
        var halfWidth = halfHeight * width / height;
        var light = new Lighting { CameraPosition = camera, Model = ShadingModel.BlinnPhong,
            DirectionToLight = Vector3.Normalize(new Vector3(-0.8f, 1.2f, -0.8f)) };
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                var sx = ((x + 0.5f) / width * 2f - 1f) * halfWidth;
                var sy = (1f - (y + 0.5f) / height * 2f) * halfHeight;
                var ray = Vector3.Normalize(forward + right * sx + up * sy);
                if (!Trace(camera, ray, out var hit)) {
                    result.SetPixel(x, y, new Color(30, 40, 60, 255));
                    continue;
                }
                // 从表面稍微向外偏移，避免阴影射线立刻撞到自己。
                var blocked = shadows && Trace(hit.Position + hit.Normal * 1e-3f,
                    light.DirectionToLight, out _);
                var color = blocked
                    ? new Color((byte)(hit.Albedo.R * light.Ambient),
                        (byte)(hit.Albedo.G * light.Ambient), (byte)(hit.Albedo.B * light.Ambient), (byte)255)
                    : light.Shade(hit.Albedo, hit.Normal, hit.Position);
                result.SetPixel(x, y, color);
            }
        }
        return result;
    }
}
