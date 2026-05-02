using Core;
using Core.Block;
using Core.Item;
using UnityEngine;

public class BlockSelector : MonoBehaviour
{

    public Camera playerCamera;

    public Vector3Int highlightedBlock;
    
    public float maxDistance = 6.0f;
    private bool hasHit;
    private Vector3Int lastHitBlockPos;
    private Vector3Int lastHitNormal;
    public Transform highlightCube;
    private ChunkManager chunkManager;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        chunkManager = FindAnyObjectByType<ChunkManager>();
        GenerateSelectorCube();
    }

    // Update is called once per frame
    void Update()
    {
        CheckForHit();
        UpdateHighLight();
    }
    
    private void CheckForHit()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        hasHit = TryGetBlockHit(ray.origin, ray.direction, maxDistance, out lastHitBlockPos, out lastHitNormal);
    }

    private void UpdateHighLight()
    {
        if (hasHit)
        {

            highlightedBlock = lastHitBlockPos;
            highlightCube.gameObject.SetActive(true);
            highlightCube.position = highlightedBlock + Vector3.one * 0.5f;
        }
        else
        {
            highlightCube.gameObject.SetActive(false);
        }
    }

    private void GenerateSelectorCube()
    {
        highlightCube = Instantiate(highlightCube);
        highlightCube.gameObject.SetActive(false);
    }
    
    private bool TryGetBlockHit(Vector3 origin, Vector3 direction, float distance, out Vector3Int hitBlockPos, out Vector3Int hitNormal)
    {
        hitBlockPos = default;
        hitNormal = default;

        if (chunkManager == null || direction.sqrMagnitude < 0.000001f)
            return false;

        Vector3 dir = direction.normalized;
        Vector3Int current = Vector3Int.FloorToInt(origin);

        int stepX = dir.x >= 0f ? 1 : -1;
        int stepY = dir.y >= 0f ? 1 : -1;
        int stepZ = dir.z >= 0f ? 1 : -1;

        float tDeltaX = dir.x == 0f ? float.PositiveInfinity : Mathf.Abs(1f / dir.x);
        float tDeltaY = dir.y == 0f ? float.PositiveInfinity : Mathf.Abs(1f / dir.y);
        float tDeltaZ = dir.z == 0f ? float.PositiveInfinity : Mathf.Abs(1f / dir.z);

        float nextBoundaryX = current.x + (stepX > 0 ? 1f : 0f);
        float nextBoundaryY = current.y + (stepY > 0 ? 1f : 0f);
        float nextBoundaryZ = current.z + (stepZ > 0 ? 1f : 0f);

        float tMaxX = dir.x == 0f ? float.PositiveInfinity : Mathf.Abs((nextBoundaryX - origin.x) / dir.x);
        float tMaxY = dir.y == 0f ? float.PositiveInfinity : Mathf.Abs((nextBoundaryY - origin.y) / dir.y);
        float tMaxZ = dir.z == 0f ? float.PositiveInfinity : Mathf.Abs((nextBoundaryZ - origin.z) / dir.z);

        if (chunkManager.GetBlockAtWorldPos(current) != 0)
        {
            hitBlockPos = current;
            hitNormal = -Vector3Int.RoundToInt(dir);
            return true;
        }

        float traveled = 0f;
        while (traveled <= distance)
        {
            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                current.x += stepX;
                traveled = tMaxX;
                tMaxX += tDeltaX;
                hitNormal = new Vector3Int(-stepX, 0, 0);
            }
            else if (tMaxY < tMaxZ)
            {
                current.y += stepY;
                traveled = tMaxY;
                tMaxY += tDeltaY;
                hitNormal = new Vector3Int(0, -stepY, 0);
            }
            else
            {
                current.z += stepZ;
                traveled = tMaxZ;
                tMaxZ += tDeltaZ;
                hitNormal = new Vector3Int(0, 0, -stepZ);
            }

            if (traveled > distance)
                break;

            if (chunkManager.GetBlockAtWorldPos(current) != 0)
            {
                hitBlockPos = current;
                return true;
            }
        }

        return false;
    }

}
