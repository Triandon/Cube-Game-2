using UnityEngine;

public static class ThreadConstants
{
    public static byte GrassID = 1;
    public static byte DirtID = 2;
    public static byte StoneID = 3;

    // Call on main thread after blocks are registered
    public static void PrepareCommonBlockIDs()
    {
        var grass = Core.Block.BlockRegistry.GetBlock("Grass_Block");
        var dirt = Core.Block.BlockRegistry.GetBlock("Dirt_Block");
        var stone = Core.Block.BlockRegistry.GetBlock("Stone_Block");

        if (grass != null) GrassID = grass.id;
        if (dirt != null) DirtID = dirt.id;
        if (stone != null) StoneID = stone.id;

        Debug.Log($"ThreadConstants prepared: grass={GrassID}, dirt={DirtID}, stone={StoneID}");
    }
}
