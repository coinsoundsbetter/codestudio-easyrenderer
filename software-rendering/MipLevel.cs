using Raylib_cs;

namespace SoftwareRendering;

public class MipLevel {
    public readonly int Width;
    public readonly int Height;
    public readonly Color[] Pixels;

    public MipLevel(int width, int height, Color[] mPixels) {
        Width = width;
        Height = height;
        Pixels = mPixels;
    }
}
