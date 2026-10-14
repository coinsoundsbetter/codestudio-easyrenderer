using System.Numerics;
using Raylib_cs;

namespace SoftwareRendering;

public class Texture {
    public int Width { get; }
    public int Height { get; }
    
    private readonly Color[] m_Pixels;
    
    private List<MipLevel> m_MipLevels;
    public int MipLevels => m_MipLevels.Count;
    
    private Texture(int width, int height, Color[] mPixels) {
        Width = width;
        Height = height;
        m_Pixels = mPixels;
        m_MipLevels = new List<MipLevel>();
        //不管开不开mipmap生成,完整贴图肯定占有一级
        m_MipLevels.Add(new MipLevel(Width, Height, m_Pixels));
    }

    public static Texture Load(string path) {
        if (!File.Exists(path)) {
            throw new FileNotFoundException("纹理文件不存在", path);
        }
        
        var image = Raylib.LoadImage(path);
        if (image.Width <= 0 || image.Height <= 0) {
            throw new InvalidDataException("无法读取纹理尺寸。");
        }
        
        try {
            var pixels = new Color[image.Width * image.Height];
            for (int y = 0; y < image.Height; y++) {
                for (int x = 0; x < image.Width; x++) {
                    pixels[y * image.Width + x] = Raylib.GetImageColor(image, x, y);
                }
            }
            return new Texture(image.Width, image.Height, pixels);
        } finally {
            Raylib.UnloadImage(image);
        }
    }

    // 从周期高度函数 h(u,v) 的斜率生成切线空间法线，无需外部法线贴图。
    public static Texture CreateNormalMap(int size = 128) {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                var u = (x + 0.5f) / size;
                var v = 1f - (y + 0.5f) / size;
                var slopeU = 0.7f * MathF.Cos(2f * MathF.PI * u) * MathF.Sin(2f * MathF.PI * v);
                var slopeV = 0.7f * MathF.Sin(2f * MathF.PI * u) * MathF.Cos(2f * MathF.PI * v);
                var normal = Vector3.Normalize(new Vector3(-slopeU, -slopeV, 1f));
                byte Encode(float component) => (byte)Math.Clamp(MathF.Round((component * 0.5f + 0.5f) * 255f), 0f, 255f);
                pixels[y * size + x] = new Color(Encode(normal.X), Encode(normal.Y), Encode(normal.Z), (byte)255);
            }
        }
        var result = new Texture(size, size, pixels);
        result.GenerateMipMaps();
        return result;
    }

    public static Texture CreateVerticalStripeTexture(int width, int height, int stripeWidth) {
        if (width <= 0 || height <= 0 || stripeWidth <= 0) {
            throw new ArgumentOutOfRangeException();
        }

        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                var isWhite = (x / stripeWidth) % 2 == 0;
                pixels[y * width + x] = isWhite ? Color.White : Color.Black;
            }
        }

        return new Texture(width, height, pixels);
    }

    public void GenerateMipMaps() {
        //各个级别的 multum-in-parvo map
        while (true) {
            //从那一级别开始生成下一级别的mipmap
            var source = m_MipLevels[m_MipLevels.Count - 1];
            if (source.Width == 1 && source.Height == 1) {
                break;
            }

            //为什么是 width+1 再除以 2?
            //原因是在宽度为奇数的时候,这样会导致某个像素被丢弃
            //eg:5->2
            /*源： [0] [1] [2] [3] [4]
            新：  [0]     [1]
            覆盖：0,1     2,3*/
            //eg:5->3
            /*源： [0] [1] [2] [3] [4]
            新：  [0]     [1]     [2]
            覆盖：0,1     2,3     4,边缘*/
            var destW = (source.Width + 1) / 2;
            var destH = (source.Height + 1) / 2;
            var destPixels = new Color[destW * destH];
            for (int y = 0; y < destH; y++) {
                for (int x = 0; x < destW; x++) {
                    //每次计算新的mipmap的时候,宽度跟高度都会缩小为上一级的一半
                    //所以相同的位置,我们要取到上一级的纹素,就需要乘以二来得到
                    destPixels[y * destW + x] = Average2X2(source, x * 2, y * 2);
                }
            }
            
            m_MipLevels.Add(new MipLevel(destW, destH, destPixels));
        }
    }

    private static Color Average2X2(MipLevel source, int x, int y) {
        //应用一种简单的方法:比如5->3的时候mipmap变成(0,1)(2,3)(4,?)
        //这里的?选择简单的重复4来填充
        var x0 = Math.Clamp(x, 0, source.Width - 1);
        var x1 = Math.Clamp(x + 1, 0, source.Width - 1);
        var y0 = Math.Clamp(y, 0, source.Height - 1);
        var y1 = Math.Clamp(y + 1, 0, source.Height - 1);
        //我们通过上一层的2x2纹素,得到这一层的颜色应该是多少(平均)
        var c00 = source.Pixels[y0 * source.Width + x0];
        var c10 = source.Pixels[y0 * source.Width + x1];
        var c01 = source.Pixels[y1 * source.Width + x0];
        var c11 = source.Pixels[y1 * source.Width + x1];
        return new Color(
            (byte)((c00.R + c10.R + c01.R + c11.R) / 4),
            (byte)((c00.G + c10.G + c01.G + c11.G) / 4),
            (byte)((c00.B + c10.B + c01.B + c11.B) / 4),
            (byte)((c00.A + c10.A + c01.A + c11.A) / 4));
    }

    public Color SampleNearest(Vector2 uv, int mipmapLevel = 0) {
        //采样方式默认使用Clamp,超出[0, 1]的uv固定在纹理边缘
        var u = Math.Clamp(uv.X, 0f, 1f);
        var v = Math.Clamp(uv.Y, 0f, 1f);

        //得到当前的mipmap等级
        mipmapLevel = Math.Clamp(mipmapLevel, 0, m_MipLevels.Count - 1);
        var level = m_MipLevels[mipmapLevel];
        
        //翻转(因为uv坐标跟屏幕坐标系y轴相反)
        v = 1f - v;
        var x = (int)MathF.Round(u * (level.Width - 1));
        var y = (int)MathF.Round(v * (level.Height - 1));

        return level.Pixels[y * level.Width + x];
    }

    public Color SampleBilinear(Vector2 uv, int mipmapLevel = 0) {
        //Clamp?
        //var u = Math.Clamp(uv.X, 0f, 1f);
        //var v = Math.Clamp(uv.Y, 0f, 1f);
        //Repeat
        var u = uv.X - MathF.Floor(uv.X);
        var v = uv.Y - MathF.Floor(uv.Y);
        
        v = 1f - v;
        
        //根据mipmap等级,决定当前分辨率
        mipmapLevel = Math.Clamp(mipmapLevel, 0, m_MipLevels.Count - 1);
        var level = m_MipLevels[mipmapLevel];
        // 纹素中心位于 (i+0.5)/尺寸；边界两侧都参与过滤，Repeat 接缝才能连续。
        var x = u * level.Width - 0.5f;
        var y = v * level.Height - 0.5f;
        //左上角纹素坐标
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var x1 = x0 + 1;
        var y1 = y0 + 1;
        /*var sampleX1 = Math.Min(x1, Width - 1);
        var sampleY1 = Math.Min(y1, Height - 1);*/
        //Repeat
        static int Wrap(int index, int length) => (index % length + length) % length;
        var sampleX0 = Wrap(x0, level.Width);
        var sampleY0 = Wrap(y0, level.Height);
        var sampleX1 = Wrap(x1, level.Width);
        var sampleY1 = Wrap(y1, level.Height);
        var dx0 = x - x0; //这个值表示靠右侧权重
        var dy0 = y - y0; //这个值表示靠下侧权重
        var dx1 = x1 - x; //这个值表示靠左侧权重
        var dy1 = y1 - y; //这个值表示靠上侧权重
        //影响权重 = 所在行的影响权重 * 所在列的影响权重
        var weightLeftTop = dx1 * dy1;
        var weightRightTop = dx0 * dy1;
        var weightLeftBottom = dx1 * dy0;
        var weightRightBottom = dx0 * dy0;
        var colorLeftTop = level.Pixels[sampleY0 * level.Width + sampleX0];
        var colorRightTop = level.Pixels[sampleY0 * level.Width + sampleX1];
        var colorLeftBottom = level.Pixels[sampleY1 * level.Width + sampleX0];
        var colorRightBottom = level.Pixels[sampleY1 * level.Width + sampleX1];
        var colorR = weightLeftTop * colorLeftTop.R +
                     weightLeftBottom * colorLeftBottom.R +
                     weightRightTop * colorRightTop.R +
                     weightRightBottom * colorRightBottom.R;
        var colorG = weightLeftTop * colorLeftTop.G +
                     weightLeftBottom * colorLeftBottom.G +
                     weightRightTop * colorRightTop.G +
                     weightRightBottom * colorRightBottom.G;
        var colorB = weightLeftTop * colorLeftTop.B +
                     weightLeftBottom * colorLeftBottom.B +
                     weightRightTop * colorRightTop.B +
                     weightRightBottom * colorRightBottom.B;
        return new Color(
            (byte)Math.Clamp(MathF.Round(colorR), 0, 255),
            (byte)Math.Clamp(MathF.Round(colorG), 0, 255),
            (byte)Math.Clamp(MathF.Round(colorB), 0, 255),
            (byte)255);
    }

    public Color SampleTrilinear(Vector2 uv, float lod) {
        //保护
        lod = Math.Clamp(lod, 0f, MipLevels - 1);
        var lowerLevel = (int)MathF.Floor(lod);
        //正常上层是lowerLevel+1,保护不超过最大级别
        var upperLevel = Math.Min(lowerLevel + 1, MipLevels - 1);
        var t = lod - lowerLevel;
        var lowerColor = SampleBilinear(uv, lowerLevel);
        var upperColor = SampleBilinear(uv, upperLevel);
        return LerpColor(lowerColor, upperColor, t);
    }

    public Color SampleFiltered(Vector2 uv, Vector2 dUvDx, Vector2 dUvDy,
        bool anisotropic, int maxAnisotropy = 8) {
        var dx = dUvDx * new Vector2(Width, Height);
        var dy = dUvDy * new Vector2(Width, Height);
        if (!anisotropic) {
            var rho = MathF.Max(dx.Length(), dy.Length());
            return SampleTrilinear(uv, MathF.Log2(MathF.Max(rho, 1f)));
        }

        // J 的两列是 dx/dy；J*J^T 的特征值给出纹素足迹的长短轴长度平方。
        var a = dx.X * dx.X + dy.X * dy.X;
        var b = dx.X * dx.Y + dy.X * dy.Y;
        var c = dx.Y * dx.Y + dy.Y * dy.Y;
        var discriminant = MathF.Sqrt((a - c) * (a - c) + 4f * b * b);
        var majorSquared = MathF.Max(0f, (a + c + discriminant) * 0.5f);
        var minorSquared = MathF.Max(0f, (a + c - discriminant) * 0.5f);
        var major = MathF.Sqrt(majorSquared);
        var minor = MathF.Sqrt(minorSquared);
        var direction = MathF.Abs(b) > 1e-8f
            ? Vector2.Normalize(new Vector2(b, majorSquared - a))
            : (a >= c ? Vector2.UnitX : Vector2.UnitY);

        // 限制采样次数；超过上限时提高 LOD，保留一些模糊以避免漏采。
        maxAnisotropy = Math.Clamp(maxAnisotropy, 1, 16);
        var footprintWidth = MathF.Max(1f, MathF.Max(minor, major / maxAnisotropy));
        var taps = Math.Clamp((int)MathF.Ceiling(major / footprintWidth), 1, maxAnisotropy);
        var lod = MathF.Log2(footprintWidth);
        var spanUv = direction * major / new Vector2(Width, Height);
        var sum = Vector4.Zero;
        for (int i = 0; i < taps; i++) {
            var offset = ((i + 0.5f) / taps - 0.5f) * spanUv;
            var sample = SampleTrilinear(uv + offset, lod);
            sum += new Vector4(sample.R, sample.G, sample.B, sample.A);
        }
        sum /= taps;
        return new Color((byte)MathF.Round(sum.X), (byte)MathF.Round(sum.Y),
            (byte)MathF.Round(sum.Z), (byte)MathF.Round(sum.W));
    }

    private static Color LerpColor(Color a, Color b, float t) {
        return new Color(
            (byte)Math.Clamp(MathF.Round(a.R + (b.R - a.R) * t), 0, 255),
            (byte)Math.Clamp(MathF.Round(a.G + (b.G - a.G) * t), 0, 255),
            (byte)Math.Clamp(MathF.Round(a.B + (b.B - a.B) * t), 0, 255),
            (byte)Math.Clamp(MathF.Round(a.A + (b.A - a.A) * t), 0, 255));
    }
}
