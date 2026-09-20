namespace SoftwareRendering;

public class Render {
    private readonly FrameBuffer m_FrameBuffer;
    private readonly DepthBuffer m_DepthBuffer;
    public Camera Camera { get; private set; }

    public Render(FrameBuffer frameBuffer, DepthBuffer depthBuffer) {
        m_FrameBuffer = frameBuffer;
        m_DepthBuffer = depthBuffer;
    }
    
    // 设置绘制相机
    public void SetCamera(Camera camera) {
        Camera = camera;
    }

    // 模拟draw-call,一个绘制模型的命令
    public void DrawModel(Model model, Texture texture) {
        //我们需要知道当前屏幕的长宽比,才能让物体保持正确的比例
        var aspectRatio = (float)m_FrameBuffer.Width / m_FrameBuffer.Height;
        
    }
    
}
