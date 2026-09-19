using System.Numerics;
using Raylib_cs;

namespace SoftwareRendering;

public class Texture {
    public int Width { get; }
    public int Height { get; }
    
    private readonly Color[] m_Pixels;
    
    private Texture(int width, int height, Color[] mPixels) {
        Width = width;
        Height = height;
        m_Pixels = mPixels;
    }

    public static Texture Load(string path) {
        if (!File.Exists(path)) {
            throw new FileNotFoundException("纹理文件不存在", path);
        }
        
        var image = Raylib.LoadImage(path);
        if (image.Width <= 0 || image.Height <= 0) {
            throw new InvalidDataException("无法读取纹理尺寸。");
        }
        
        var pixels = new Color[image.Width * image.Height];
        for (int y = 0; y < image.Height; y++) {
            for (int x = 0; x < image.Width; x++) {
                pixels[y * image.Width + x] = Raylib.GetImageColor(image, x, y);
            }
        }
        
        return new Texture(image.Width, image.Height, pixels);
    }

    public static Texture CreateCheckerboard(
        int width,
        int height,
        int cellSize,
        Color lightColor,
        Color darkColor) {
        if (width <= 0 || height <= 0 || cellSize <= 0) {
            throw new ArgumentOutOfRangeException(
                "棋盘格的尺寸与格子大小必须为正数。");
        }

        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                var isLight = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                pixels[y * width + x] = isLight ? lightColor : darkColor;
            }
        }

        return new Texture(width, height, pixels);
    }

    public Color SampleNearest(Vector2 uv) {
        //采样方式默认使用Clamp,超出[0, 1]的uv固定在纹理边缘
        var u = Math.Clamp(uv.X, 0f, 1f);
        var v = Math.Clamp(uv.Y, 0f, 1f);

        //翻转?
        v = 1f - v;

        var x = (int)MathF.Round(u * (Width - 1));
        var y = (int)MathF.Round(v * (Height - 1));
        return m_Pixels[y * Width + x];
    }

    public Color SampleBilinear(Vector2 uv) {
        //Clamp?
        /*var u = Math.Clamp(uv.X, 0f, 1f);
        var v = Math.Clamp(uv.Y, 0f, 1f);*/
        //Repeat
        var u = uv.X - MathF.Floor(uv.X);
        var v = uv.Y - MathF.Floor(uv.Y);
        
        v = 1f - v;

        var x = u * (Width - 1);
        var y = v * (Height - 1);
        //左上角纹素坐标
        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = x0 + 1;
        var y1 = y0 + 1;
        // Repeat 模式下，右/下邻居越过边缘时回到第 0 列/行。
        var sampleX1 = x1 % Width;
        var sampleY1 = y1 % Height;
        var dx0 = x - x0; //这个值表示靠右侧权重
        var dy0 = y - y0; //这个值表示靠下侧权重
        var dx1 = x1 - x; //这个值表示靠左侧权重
        var dy1 = y1 - y; //这个值表示靠上侧权重
        //影响权重 = 所在行的影响权重 * 所在列的影响权重
        var weightLeftTop = dx1 * dy1;
        var weightRightTop = dx0 * dy1;
        var weightLeftBottom = dx1 * dy0;
        var weightRightBottom = dx0 * dy0;
        var colorLeftTop = m_Pixels[y0 * Width + x0];
        var colorRightTop = m_Pixels[y0 * Width + sampleX1];
        var colorLeftBottom = m_Pixels[sampleY1 * Width + x0];
        var colorRightBottom = m_Pixels[sampleY1 * Width + sampleX1];
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
}
