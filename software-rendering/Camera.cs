using System.Numerics;

namespace SoftwareRendering;

public class Camera {
    public Vector3 Position;
    public Vector3 Forward;
    public Vector3 Up;
    public float FieldOfView;
    public float NearPlane;
    public float FarPlane;

    public Matrix4x4 GetViewMatrix() {
        return Matrix4x4.CreateLookAt(Position, Position + Forward, Up);
    }

    public Matrix4x4 GetProjectionMatrix(float aspectRatio) {
        return Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfView,
            aspectRatio,
            NearPlane,
            FarPlane);
    } 
}