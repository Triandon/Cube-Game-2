using UnityEngine;

public class VoxelData
{
    
    // +X (right)
    private static readonly Vector3[] FaceRight =
    {
        new Vector3(1, 0, 1),
        new Vector3(1, 0, 0),
        new Vector3(1, 1, 1),
        new Vector3(1, 1, 0)
    };

    // -X (left)
    private static readonly Vector3[] FaceLeft =
    {
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 1),
        new Vector3(0, 1, 0),
        new Vector3(0, 1, 1)
    };

    // +Y (top)
    private static readonly Vector3[] FaceUp =
    {
        new Vector3(0, 1, 0),
        new Vector3(0, 1, 1),
        new Vector3(1, 1, 0),
        new Vector3(1, 1, 1)
    };

    // -Y (bottom)
    private static readonly Vector3[] FaceDown =
    {
        new Vector3(0, 0, 1),
        new Vector3(0, 0, 0),
        new Vector3(1, 0, 1),
        new Vector3(1, 0, 0)
    };

    // +Z (forward)
    private static readonly Vector3[] FaceForward =
    {
        new Vector3(0, 0, 1),
        new Vector3(1, 0, 1),
        new Vector3(0, 1, 1),
        new Vector3(1, 1, 1)
    };

    // -Z (back)
    private static readonly Vector3[] FaceBack =
    {
        new Vector3(1, 0, 0),
        new Vector3(0, 0, 0),
        new Vector3(1, 1, 0),
        new Vector3(0, 1, 0)
    };

    private static readonly Vector3[] EmptyFace = new Vector3[4];

    public static Vector3[] GetFaceVertices(Vector3 normal)
    {
        if (normal == Vector3.right) return FaceRight;
        if (normal == Vector3.left) return FaceLeft;
        if (normal == Vector3.up) return FaceUp;
        if (normal == Vector3.down) return FaceDown;
        if (normal == Vector3.forward) return FaceForward;
        if (normal == Vector3.back) return FaceBack;
        return EmptyFace;
    }

}
