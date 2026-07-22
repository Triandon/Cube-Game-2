using UnityEngine;

public static class VoxelLight
{
    public const byte Min = 0;
    public const byte Max = 15;

    public static byte Pack(byte skyLight, byte blockLight)
    {
        return (byte)((skyLight << 4) | blockLight);
    }
    
    public static byte GetSky(byte packed)
    {
        return (byte)(packed << 4);
    }
    
    public static byte GetBlock(byte packed)
    {
        return (byte)(packed & 0x0F);
    }
}
