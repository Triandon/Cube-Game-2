using System.Collections.Generic;
using Core.Block;
using UnityEngine;

public static class ItemMeshBuilder
{
    private const int ATLAS_TILES = 16;

    public static Mesh BuildBlockItemMesh(Block block)
    {
        Mesh mesh = new Mesh();

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();

        float size = 1f;
        Vector3[] faceDirs =
        {
            Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back,
            Vector3.right, Vector3.left
        };

        foreach (var dir in faceDirs)
        {
            int texIndex =
                dir == Vector3.up ? block.topIndex :
                dir == Vector3.down ? block.bottomIndex :
                block.sideIndex;

            AddFace(vertices, triangles, uvs, dir, size, texIndex);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
    
    private static void AddFace(
        List<Vector3> verts,
        List<int> tris,
        List<Vector2> uvs,
        Vector3 dir,
        float size,
        int atlasIndex)
    {
        int start = verts.Count;

        Vector3[] quad = VoxelData.GetFaceVertices(Vector3Int.RoundToInt(dir));

        for (int i = 0; i < 4; i++)
            verts.Add((quad[i] - Vector3.one * 0.5f) * size);

        tris.Add(start + 0);
        tris.Add(start + 1);
        tris.Add(start + 2);
        tris.Add(start + 2);
        tris.Add(start + 1);
        tris.Add(start + 3);

        // --- Atlas UV ---
        float tile = 1f / ATLAS_TILES;
        int x = atlasIndex % ATLAS_TILES;
        int y = atlasIndex / ATLAS_TILES;

        float uMin = x * tile;
        float vMin = 1f - (y + 1) * tile;
        float vMax = vMin + tile;

        uvs.Add(new Vector2(uMin, vMin));
        uvs.Add(new Vector2(uMin + tile, vMin));
        uvs.Add(new Vector2(uMin, vMax));
        uvs.Add(new Vector2(uMin + tile, vMax));
    }
}
