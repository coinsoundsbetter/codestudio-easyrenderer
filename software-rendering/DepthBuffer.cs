namespace SoftwareRendering;

public class DepthBuffer {
    private readonly float[] m_Depths;
    private readonly int m_Width;
    private readonly int m_Height;

    public DepthBuffer(int width, int height) {
        m_Width = width;
        m_Height = height;
        m_Depths = new float[width * height];
    }

    public float GetDepth(int x, int y) {
        return m_Depths[y * m_Width + x];
    }

    public void SetDepth(int x, int y, float depth) {
        if (x < 0 || x >= m_Width || y < 0 || y >= m_Height) {
            return;
        }
        
        m_Depths[y * m_Width + x] = depth;
    }

    public void Clear(float depth) {
        for (int i = 0; i < m_Depths.Length; i++) {
            m_Depths[i] = depth;
        }
    }
}
