using System.Numerics;

namespace SoftwareRendering;

public enum RotationMode { Euler, QuaternionLocal, QuaternionWorld }

// 将输入与数学计算分开，便于在无窗口验证中检查顺序和万向锁。
public class RotationStudy {
    public RotationMode Mode;
    public float Yaw, Pitch, Roll;
    public Quaternion Orientation = Quaternion.Identity;
    private Quaternion resetStart;
    private float resetTime = 1f;
    public bool IsResetting => resetTime < 1f;

    // 行向量从左到右：先 roll(Z)，再 pitch(X)，再 yaw(Y)。
    public static Matrix4x4 EulerMatrix(float yaw, float pitch, float roll) =>
        Matrix4x4.CreateRotationZ(roll) * Matrix4x4.CreateRotationX(pitch) * Matrix4x4.CreateRotationY(yaw);

    public Matrix4x4 Matrix => Mode == RotationMode.Euler
        ? EulerMatrix(Yaw, Pitch, Roll) : Matrix4x4.CreateFromQuaternion(Orientation);

    public void SetMode(RotationMode mode) {
        // 切换到四元数时保留当前姿态；返回 Euler 实验时重置，避免不明确的角度提取。
        var previous = Matrix;
        Mode = mode;
        if (mode == RotationMode.Euler) { Yaw = Pitch = Roll = 0f; }
        else Orientation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(previous));
        resetTime = 1f;
    }

    public void SetGimbalPose() {
        Mode = RotationMode.Euler;
        Yaw = Roll = 0f;
        Pitch = MathF.PI / 2f;
        resetTime = 1f;
    }

    public void BeginReset() {
        resetStart = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(Matrix));
        // 直接插值四元数，而不是把它转回欧拉角再插值。
        Mode = RotationMode.QuaternionLocal;
        Orientation = resetStart;
        resetTime = 0f;
    }

    public void Update(float deltaTime, float yawDelta, float pitchDelta, float rollDelta) {
        if (IsResetting) {
            resetTime = MathF.Min(1f, resetTime + deltaTime);
            Orientation = Quaternion.Normalize(Quaternion.Slerp(resetStart, Quaternion.Identity, resetTime));
            return;
        }
        if (Mode == RotationMode.Euler) {
            Yaw += yawDelta; Pitch += pitchDelta; Roll += rollDelta;
        } else {
            var delta = Quaternion.CreateFromYawPitchRoll(yawDelta, pitchDelta, rollDelta);
            // System.Numerics：M(q1*q2) = M(q2)*M(q1)。
            // 局部轴增量先作用于点；世界轴增量最后作用于点。
            Orientation = Quaternion.Normalize(Mode == RotationMode.QuaternionLocal
                ? Orientation * delta : delta * Orientation);
        }
    }
}
