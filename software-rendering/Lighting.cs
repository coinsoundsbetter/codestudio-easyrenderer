using System.Numerics;
using Raylib_cs;

namespace SoftwareRendering;

public enum ShadingModel { Lambert, Phong, BlinnPhong }

// 学习用的经验光照模型。所有方向、位置均使用世界空间。
public class Lighting {
    public Vector3 DirectionToLight = Vector3.Normalize(new Vector3(0f, 1f, -1f));
    public Vector3 CameraPosition;
    public float Ambient = 0.15f;
    public float DiffuseStrength = 0.75f;
    public float SpecularStrength = 0.45f;
    public float PhongExponent = 24f;
    // 同一高光宽度下 Blinn 指数通常比 Phong 大；这里用约四倍作演示。
    public float BlinnExponent = 96f;
    public bool Enabled = true;
    public ShadingModel Model = ShadingModel.Lambert;
    public bool UseNormalMap;
    public Texture? NormalMap;
    public float NormalMapTiling = 8f;
    public float NormalMapStrength = 0.65f;
    public bool UseAnisotropic;
    public bool UseCorrectNormalMatrix = true;

    public static Vector3 NormalizeOr(Vector3 value, Vector3 fallback) {
        return value.LengthSquared() > 1e-12f ? Vector3.Normalize(value) : fallback;
    }

    public Vector3 ApplyNormalMap(Vector3 worldNormal, Vector4 worldTangent,
        Vector2 uv, Vector2 dUvDx, Vector2 dUvDy) {
        var n = NormalizeOr(worldNormal, Vector3.UnitY);
        if (!UseNormalMap || NormalMap is null) return n;

        // T 沿 UV 的 +U；先去掉它沿 N 的分量，使插值后的 T 与 N 垂直。
        var t = new Vector3(worldTangent.X, worldTangent.Y, worldTangent.Z);
        t -= n * Vector3.Dot(n, t);
        if (t.LengthSquared() < 1e-12f) return n; // 缺少有效 UV/切线时保留几何法线。
        t = Vector3.Normalize(t);
        var sign = worldTangent.W < 0f ? -1f : 1f;
        var b = Vector3.Cross(n, t) * sign; // B 沿 UV 的 +V，兼容镜像 UV。

        var sample = NormalMap.SampleFiltered(uv * NormalMapTiling,
            dUvDx * NormalMapTiling, dUvDy * NormalMapTiling, UseAnisotropic);
        // RGB [0,255] 编码切线空间法线 [-1,1]。法线数据不做 Gamma 解码。
        var tangentNormal = new Vector3(sample.R / 255f * 2f - 1f,
            sample.G / 255f * 2f - 1f, sample.B / 255f * 2f - 1f);
        tangentNormal.X *= NormalMapStrength;
        tangentNormal.Y *= NormalMapStrength;
        tangentNormal = NormalizeOr(tangentNormal, Vector3.UnitZ);
        return NormalizeOr(t * tangentNormal.X + b * tangentNormal.Y + n * tangentNormal.Z, n);
    }

    public Color Shade(Color albedo, Vector3 worldNormal, Vector3 worldPosition) {
        if (!Enabled) return albedo;
        var n = NormalizeOr(worldNormal, Vector3.UnitY);
        var l = NormalizeOr(DirectionToLight, Vector3.Zero);
        var ndotl = MathF.Max(0f, Vector3.Dot(n, l));
        var specular = 0f;
        // 光在表面背后时不产生高光，避免背光漏亮。
        if (ndotl > 0f && Model != ShadingModel.Lambert) {
            var v = NormalizeOr(CameraPosition - worldPosition, Vector3.Zero);
            if (Model == ShadingModel.Phong) {
                // Reflect 的输入是射向表面的方向，因此传 -L。
                var r = Vector3.Reflect(-l, n);
                specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(r, v)), PhongExponent);
            } else {
                var h = NormalizeOr(l + v, Vector3.Zero);
                specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(n, h)), BlinnExponent);
            }
        }
        var diffuseBrightness = Ambient + DiffuseStrength * ndotl;
        // 白色高光单独相加，不染上纹理色；最后限制在显示范围内。
        byte Channel(byte channel) => (byte)Math.Clamp(MathF.Round(
            channel * diffuseBrightness + 255f * SpecularStrength * specular), 0f, 255f);
        return new Color(Channel(albedo.R), Channel(albedo.G), Channel(albedo.B), albedo.A);
    }
}
