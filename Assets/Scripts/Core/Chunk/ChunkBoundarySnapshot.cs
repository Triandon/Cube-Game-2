using UnityEngine;

public sealed class ChunkBoundarySnapshot<T>
{
    public T[] PositiveX;
    public T[] NegativeX;
    public T[] PositiveY;
    public T[] NegativeY;
    public T[] PositiveZ;
    public T[] NegativeZ;

    public bool IsEmpty => PositiveX == null && NegativeX == null &&
                           PositiveY == null && NegativeY == null &&
                           PositiveZ == null && NegativeZ == null;
    
}
