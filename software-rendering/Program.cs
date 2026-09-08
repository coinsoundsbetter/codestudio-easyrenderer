using Raylib_cs;

namespace SoftwareRendering;

internal static class Program
{
    [STAThread]
    private static void Main() {
        var width = 800;
        var height = 600;
        Raylib.InitWindow(width, height, "Software Rendering");
        Raylib.SetTargetFPS(60);

        var frameBuffer = new FrameBuffer(width, height);
        var depthBuffer = new DepthBuffer(width, height);

        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);
            
            depthBuffer.Clear(float.MaxValue);

            var nearTriangle = new Triangle
            {
                V0 = new Vertex { X = 180, Y = 120, Z = 0.1f, Color = Color.Red },
                V1 = new Vertex { X = 350, Y = 480, Z = 0.9f, Color = Color.Red },
                V2 = new Vertex { X = 620, Y = 180, Z = 0.2f, Color = Color.Red },
            };
            
            Rasterizer.DrawTriangle(frameBuffer, depthBuffer, nearTriangle);
            
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    var pixel = frameBuffer.GetPixel(x, y);
                    Raylib.DrawPixel(x, y, pixel);
                }
            }
            
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
