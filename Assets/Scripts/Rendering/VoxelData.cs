using UnityEngine;

public class VoxelData
{

    public static Vector3[] GetFaceVertices(Vector3 normal)
    {
        // +X (right)
        if (normal == Vector3.right)
            return new Vector3[]
            {
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 0),
                new Vector3(1, 1, 1),
                new Vector3(1, 1, 0)
            };

        // -X (left)
        if (normal == Vector3.left)
            return new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(0, 0, 1),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 1)
            };

        // +Y (top)
        if (normal == Vector3.up)
            return new Vector3[]
            {
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 1),
                new Vector3(1, 1, 0),
                new Vector3(1, 1, 1)
            };

        // -Y (bottom)
        if (normal == Vector3.down)
            return new Vector3[]
            {
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 0)
            };

        // +Z (forward)
        if (normal == Vector3.forward)
            return new Vector3[]
            {
                new Vector3(0, 0, 1),
                new Vector3(1, 0, 1),
                new Vector3(0, 1, 1),
                new Vector3(1, 1, 1)
            };

        // -Z (back)
        if (normal == Vector3.back)
            return new Vector3[]
            {
                new Vector3(1, 0, 0),
                new Vector3(0, 0, 0),
                new Vector3(1, 1, 0),
                new Vector3(0, 1, 0)
            };

        // default fallback
        return new Vector3[4];
    }
    
}
