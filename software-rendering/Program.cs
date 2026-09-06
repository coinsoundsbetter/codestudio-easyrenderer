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

        var buffer = new FrameBuffer(width, height);

        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);

            var triangle = new Triangle
            {
                V0 = new Vertex { X = 150, Y = 100, Color = Color.Red },
                V1 = new Vertex { X = 650, Y = 150, Color = Color.Green },
                V2 = new Vertex { X = 400, Y = 500, Color = Color.Blue },
            };
            
            Rasterizer.DrawTriangle(buffer, triangle, Color.Red);
            
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    var pixel = buffer.GetPixel(x, y);
                    Raylib.DrawPixel(x, y, pixel);
                }
            }
            
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
