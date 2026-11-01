using System.Collections.Generic;
using System.Globalization;
using Core.Block;
using Core.TileEntities;
using UnityEngine;

namespace Core.Blocks
{
    public class TreeLog : Block.Block
    {
        public TreeLog(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            SetDefaultState(TreeTrunkTile.CreateSegmentState());
            isTransparent = true;
            isStrechy = true;
            isCentered = true;
        }

        // Check the tree trunk tile for func
    }
}
