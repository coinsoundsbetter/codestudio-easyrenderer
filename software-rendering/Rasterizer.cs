using Raylib_cs;

namespace SoftwareRendering;

public class Rasterizer {

    // 绘制(填充)一个三角形
    public static void DrawTriangle(
        FrameBuffer frameBuffer, 
        DepthBuffer depthBuffer,
        Triangle triangle) {
        
        //平行四边形的面积 = (v0->v1)与(v0->v2)的叉积和
        var v0 = triangle.V0;
        var v1 = triangle.V1;
        var v2 = triangle.V2;
        var area = Utils.Edge(v0, v1, v2.X, v2.Y);
        
        //三点共线或重合
        if (MathF.Abs(area) < 1e-6f) {
            return;
        }
        
        var boundingBox = Utils.GetBoundingBox(triangle);
        for (int y = boundingBox.MinY; y < boundingBox.MaxY; y++) {
            for (int x = boundingBox.MinX; x < boundingBox.MaxX; x++) {
                //对于当前屏幕坐标的约定是左上角为(0,0),x向右增大,y向下增大
                //x/y分别表示像素格左上角的坐标,要得到像素格子中心,两个方向都要移动半个像素
                //得到像素中心,用来更精确地计算几何关系
                var px = x + 0.5f;
                var py = y + 0.5f;
                var area0 = Utils.Edge(v1, v2, px, py) / area;
                var area1 = Utils.Edge(v2, v0, px, py) / area;
                var area2 = Utils.Edge(v0, v1, px, py) / area;
                //这里其实不用重心坐标,也一样能得到是否inside
                //只是后续的步骤要用重心坐标,所以就顺便用它判断里外关系,是一样的结果
                bool inside = area0 >= 0 && area1 >= 0 && area2 >= 0;
                if (!inside) {
                    continue;
                }
                
                //与深度缓冲区进行比较,被遮挡了就直接丢弃(当前暂无考虑透明物体)
                var depth = area0 * v0.Z + area1 * v1.Z + area2 * v2.Z;
                if (depthBuffer.GetDepth(x, y) < depth) {
                    continue;
                }
                
                depthBuffer.SetDepth(x, y, depth);
                
                //根据当前坐标与三个顶点之间的关系(也就是面积比例),插值得到当前的rgba
                var drawColor = new Color(
                    (byte)(area0 * v0.Color.R + area1 * v1.Color.R + area2 * v2.Color.R),
                    (byte)(area0 * v0.Color.G + area1 * v1.Color.G + area2 * v2.Color.G),
                    (byte)(area0 * v0.Color.B + area1 * v1.Color.B + area2 * v2.Color.B),
                    (byte)(area0 * v0.Color.A + area1 * v1.Color.A + area2 * v2.Color.A));
                frameBuffer.SetPixel(x, y, drawColor);    
            }
        }
    }
}
