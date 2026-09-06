using Raylib_cs;

namespace SoftwareRendering;

public class FrameBuffer {
    private Color[] m_Pixels;
    private int m_Width;
    private int m_Height;
    
    public FrameBuffer(int width, int height) {
        m_Pixels = new Color[width * height];
        m_Width = width;
        m_Height = height;
    }
    
    public void SetPixel(int x, int y, Color color) {
        if (x < 0 || x >= m_Width || y < 0 || y >= m_Height) {
            throw new ArgumentOutOfRangeException();
        }
        
        var index = y * m_Width + x;
        m_Pixels[index] = color;
    }

    public Color GetPixel(int x, int y) {
        if (x < 0 || x >= m_Width || y < 0 || y >= m_Height) {
            throw new ArgumentOutOfRangeException();
        }
        
        return m_Pixels[y * m_Width + x];
    }

    public void Clear(Color color) {
        for (int i = 0; i < m_Pixels.Length; i++) {
            m_Pixels[i] = color;
        }
    }
}
