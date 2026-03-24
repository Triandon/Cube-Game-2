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
    private RaycastHit lastHit;
    public Transform highlightCube;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            lastHit = hit;
            hasHit = true;
        }
        else
        {
            hasHit = false;
        }
    }

    private void UpdateHighLight()
    {
        if (hasHit)
        {

            highlightedBlock = GetTargetBlockPos(lastHit, false);
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
    
    private Vector3Int GetTargetBlockPos(RaycastHit hit, bool place)
    {
        const float mineOffset = 0.01f;
        Vector3 pos = hit.point - hit.normal * mineOffset;
        Vector3Int hitBlockPos = Vector3Int.FloorToInt(pos);
        
        if (!place)
            return hitBlockPos;

        Vector3Int placeOffset = new Vector3Int(
            Mathf.RoundToInt(hit.normal.x),
            Mathf.RoundToInt(hit.normal.y),
            Mathf.RoundToInt(hit.normal.z));

        
        return hitBlockPos + placeOffset;
    }

}
