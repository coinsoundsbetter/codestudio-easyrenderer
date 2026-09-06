using Raylib_cs;

namespace SoftwareRendering;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Raylib.InitWindow(800, 600, "Software Rendering");
        Raylib.SetTargetFPS(60);

        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);

            Raylib.DrawText("Start here: write your rendering pipeline.", 20, 20, 20, Color.DarkGray);

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
