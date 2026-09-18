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

    // 用于观察纹理坐标插值；相邻格使用足够高的对比度，便于看出拉伸。
    public static Texture CreateCheckerboard(int size, int cellsPerAxis) {
        if (size <= 0 || cellsPerAxis <= 0 || size % cellsPerAxis != 0) {
            throw new ArgumentOutOfRangeException();
        }

        var pixels = new Color[size * size];
        var cellSize = size / cellsPerAxis;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                var cellX = x / cellSize;
                var cellY = y / cellSize;
                var isLight = (cellX + cellY) % 2 == 0;
                pixels[y * size + x] = isLight ? Color.RayWhite : Color.DarkBlue;
            }
        }

        return new Texture(size, size, pixels);
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
}
