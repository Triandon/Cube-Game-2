using Core.Block;
using UnityEngine;

public static class BlockId
{
    public static byte Grass;
    public static byte Dirt;
    public static byte Stone;
    
    public static void Init()
    {
        Grass = BlockRegistry.GetBlock("Grass_Block").id;
        Dirt = BlockRegistry.GetBlock("Dirt_Block").id;
        Stone = BlockRegistry.GetBlock("Stone_Block").id;
    }
}
