using UnityEngine;

public struct BlockInfo
{
    public int topIndex;
    public int sideIndex;
    public int bottomIndex;
    public bool isTransparent;
    public byte id;

    public BlockInfo(byte id, int top, int side, int bottom, bool transparent)
    {
        this.id = id;
        topIndex = top;
        sideIndex = side;
        bottomIndex = bottom;
        isTransparent = transparent;
    }
}
