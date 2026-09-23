using System.Runtime.CompilerServices;

namespace Core
{
    public static class ArrayIndexing
    {
        //Just a helper for chunk data.
        // blocks[S,S,S] => blocks[s*s*s]
        // hopfully saves some peformacne 3D array to 1D

        public const int S = Chunk.CHUNK_SIZE;
        public const int Areal = S * S;
        public const int Volume = Areal * S;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToIndex(int x, int y, int z)
        {
            return x + S * (y + S * z);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToIndex(int x, int y, int z, int size)
        {
            return x + size * (y + size * z);
        }
    }
}
