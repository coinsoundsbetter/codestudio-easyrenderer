using Raylib_cs;

namespace SoftwareRendering;

public class Rasterizer {

    // 绘制(填充)一个三角形
    public static void DrawTriangle(
        FrameBuffer frameBuffer, 
        Triangle triangle,
        Color color) {
        var boundingBox = Utils.GetBoundingBox(triangle);
        for (int y = boundingBox.MinY; y < boundingBox.MaxY; y++) {
            for (int x = boundingBox.MinX; x < boundingBox.MaxX; x++) {
                var checkPx = x + 0.5f;
                var checkPy = y + 0.5f;
                if (Utils.IsInTriangle(checkPx, checkPy, triangle)) {
                    frameBuffer.SetPixel(x, y, color);    
                }
            }
        }
    }
}
