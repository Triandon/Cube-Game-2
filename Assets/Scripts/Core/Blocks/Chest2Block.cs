using Core;
using Core.Block;
using Core.Item;
using Player;
using UnityEngine;

public class Chest2Block : Block
{
    public Chest2Block(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
    {
        SetDefaultState(new BlockStateContainer().With(
            BlockStateKeys.DirectionalFacing, DirectionalFacing.North));
    }

    public override BlockStateContainer GetStateForPlacement(BlockPlacementContext context)
    {
        return CreateDefaultState().With(BlockStateKeys.DirectionalFacing,
            GetHorizontalFacingTowardPlayer(context.Player));
    }

    public override bool OnActivated(Vector3Int position, BlockStateContainer state, Block block, Transform player)
    {
        ChunkManager cm = Object.FindAnyObjectByType<ChunkManager>();
        Chest2Tile tile = cm != null ? cm.GetTileEntity<Chest2Tile>(position) : null;
        if (tile == null || tile.IsRemoved)
            return false;

        PlayerEntity playerE = player.GetComponent<PlayerEntity>();
        ItemStack heldStack = playerE != null
            ? playerE.GetHeldItemStack()
            : ItemStack.Empty;
        
        if (heldStack == null || heldStack.IsEmpty)
            InventoryEvents.Open(tile);
        
        return true;
    }
}
