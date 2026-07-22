using Core.Block;
using UnityEngine;

public static class VoxelLight
{
    public const byte Min = 0;
    public const byte Max = 15;

    public static byte Pack(byte skyLight, byte blockLight)
    {
        return (byte)(((skyLight & 0x0F) << 4) | (blockLight & 0x0F));
    }
    
    public static byte GetSky(byte packed)
    {
        return (byte)((packed >> 4) & 0x0F);
    }
    
    public static byte GetBlock(byte packed)
    {
        return (byte)(packed & 0x0F);
    }
    
    public static byte GetHighest(byte skyLight, byte blockLight)
    {
        return skyLight > blockLight ? skyLight : blockLight;
    }

    public static bool BlocksSkyLight(byte blockId)
    {
        if (blockId == 0)
            return false;

        Block block = BlockRegistry.GetBlock(blockId);
        return block == null || !block.isTransparent;
    }

}
