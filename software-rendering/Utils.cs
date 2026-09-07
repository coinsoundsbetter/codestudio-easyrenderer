using System.Numerics;

namespace SoftwareRendering;

public class Utils {

    // U x V = UxVy - UyVx
    // A->B 与 A->P 叉积
    public static float Edge(Vertex a, Vertex b, float px, float py) {
        return (b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X);
    }
    
    // 判断一个像素中心是否在定义的三角形范围内
    public static bool IsInTriangle(float px, float py, Triangle triangle) {
        var v0 = triangle.V0;
        var v1 = triangle.V1;
        var v2 = triangle.V2;

        Vector2 line01 = new Vector2(v1.X - v0.X, v1.Y - v0.Y);
        Vector2 line12 = new Vector2(v2.X - v1.X, v2.Y - v1.Y);
        Vector2 line20 = new Vector2(v0.X - v2.X, v0.Y - v2.Y);
        Vector2 linep0 = new Vector2(v0.X - px, v0.Y - py);
        Vector2 linep1 = new Vector2(v1.X - px, v1.Y - py);
        Vector2 linep2 = new Vector2(v2.X - px, v2.Y - py);

        //叉乘
        // 0->1 p->1; 
        // 1->2 p->2;
        // 2->0 p->0;
        var crossA = Vector2.Cross(line01, linep1);
        var crossB = Vector2.Cross(line12, linep2);
        var crossC = Vector2.Cross(line20, linep0);
        bool allNonNegative = crossA >= 0 && crossB >= 0 && crossC >= 0;
        bool allNonPositive = crossA <= 0 && crossB <= 0 && crossC <= 0;
        return allNonNegative || allNonPositive;
    }

    public static BoundingBox2D GetBoundingBox(Triangle triangle) {
        var minX = triangle.V0.X;
        var minY = triangle.V0.Y;
        var maxX = triangle.V0.X;
        var maxY = triangle.V0.Y;
        
        if (triangle.V1.X < minX) minX = triangle.V1.X;
        if (triangle.V2.X < minX) minX = triangle.V2.X;
        if (triangle.V1.Y < minY) minY = triangle.V1.Y;
        if (triangle.V2.Y < minY) minY = triangle.V2.Y;
        
        if (triangle.V1.X > maxX) maxX = triangle.V1.X;
        if (triangle.V2.X > maxX) maxX = triangle.V2.X;
        if (triangle.V1.Y > maxY) maxY = triangle.V1.Y;
        if (triangle.V2.Y > maxY) maxY = triangle.V2.Y;

        return new BoundingBox2D() {
            MinX = (int)MathF.Floor(minX),
            MinY = (int)MathF.Floor(minY),
            MaxX = (int)MathF.Ceiling(maxX),
            MaxY = (int)MathF.Ceiling(maxY),
        };
    }
}
