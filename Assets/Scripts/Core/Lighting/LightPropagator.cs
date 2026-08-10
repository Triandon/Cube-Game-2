using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Storage-independent flood-fill implementation for voxel sky and block light.
/// Coordinates passed to this class are world voxel coordinates.
/// </summary>
public sealed class LightPropagator
{
    public enum Channel : byte
    {
        Sky,
        Block
    }

    public interface ILightWorld
    {
        IEnumerable<Vector3Int> GetSkyLightSeeds();
        void ClearLight(Channel channel);
        bool TryGetVoxel(Vector3Int position, out byte blockId);
        byte GetLight(Vector3Int position, Channel channel);
        void SetLight(Vector3Int position, Channel channel, byte value);
    }

    private struct RemovalNode
    {
        public Vector3Int Position;
        public byte PreviousLight;

        public RemovalNode(Vector3Int position, byte previousLight)
        {
            Position = position;
            PreviousLight = previousLight;
        }
    }

    private static readonly Vector3Int[] Directions =
    {
        Vector3Int.left, Vector3Int.right, Vector3Int.down,
        Vector3Int.up, Vector3Int.back, Vector3Int.forward
    };

    private readonly ILightWorld world;
    private readonly Dictionary<Vector3Int, byte> blockLightSources = new Dictionary<Vector3Int, byte>();

    public bool HasBlockLightSources => blockLightSources.Count > 0;

    public LightPropagator(ILightWorld world)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// Rebuilds sunlight for the complete loaded voxel domain. The world provides
    /// terrain-exposed seed cells. Direct rays stay at level 15 while indirect light
    /// attenuates by one voxel per step.
    public void RebuildSkyLight()
    {
        Queue<Vector3Int> propagation = new Queue<Vector3Int>();

        world.ClearLight(Channel.Sky);

        // The world supplies only cells which are genuinely exposed to the sky;
        // an unloaded chunk above a cave must not become an implicit light source.
        foreach (Vector3Int top in world.GetSkyLightSeeds())
        {
            if (!IsTransparent(top))
                continue;

            Vector3Int position = top;
            while (IsTransparent(position))
            {
                if (world.GetLight(position, Channel.Sky) != VoxelLight.Max)
                {
                    world.SetLight(position, Channel.Sky, VoxelLight.Max);
                    propagation.Enqueue(position);
                }

                position += Vector3Int.down;
            }
        }

        Propagate(propagation, Channel.Sky, true);
    }

    /// <summary>Adds or strengthens a block-light source and propagates it.</summary>
    public void AddBlockLight(Vector3Int position, byte level = VoxelLight.Max)
    {
        if (!world.TryGetVoxel(position, out _))
            return;

        level = (byte)Mathf.Clamp(level, VoxelLight.Min, VoxelLight.Max);
        blockLightSources[position] = level;
        if (level <= world.GetLight(position, Channel.Block))
            return;

        world.SetLight(position, Channel.Block, level);
        Queue<Vector3Int> propagation = new Queue<Vector3Int>();
        propagation.Enqueue(position);
        Propagate(propagation, Channel.Block, false);
    }

    /// <summary>
    /// Removes the light rooted at a voxel, then re-propagates neighboring light so
    /// overlapping sources continue to illuminate the cleared volume.
    /// </summary>
    public void RemoveBlockLight(Vector3Int position)
    {
        blockLightSources.Remove(position);
        byte oldLevel = world.GetLight(position, Channel.Block);
        if (oldLevel == VoxelLight.Min)
            return;

        Queue<RemovalNode> removal = new Queue<RemovalNode>();
        Queue<Vector3Int> propagation = new Queue<Vector3Int>();
        world.SetLight(position, Channel.Block, VoxelLight.Min);
        removal.Enqueue(new RemovalNode(position, oldLevel));

        while (removal.Count > 0)
        {
            RemovalNode node = removal.Dequeue();
            foreach (Vector3Int direction in Directions)
            {
                Vector3Int neighbor = node.Position + direction;
                if (!world.TryGetVoxel(neighbor, out _))
                    continue;

                byte neighborLevel = world.GetLight(neighbor, Channel.Block);
                if (neighborLevel != VoxelLight.Min && neighborLevel < node.PreviousLight)
                {
                    world.SetLight(neighbor, Channel.Block, VoxelLight.Min);
                    removal.Enqueue(new RemovalNode(neighbor, neighborLevel));
                }
                else if (neighborLevel >= node.PreviousLight)
                {
                    propagation.Enqueue(neighbor);
                }
            }
        }

        Propagate(propagation, Channel.Block, false);

        // A weaker source can be cleared as part of a stronger source's removal.
        // Re-seeding known sources repairs those overlapping gradients.
        Queue<Vector3Int> sourcePropagation = new Queue<Vector3Int>();
        foreach (KeyValuePair<Vector3Int, byte> source in blockLightSources)
        {
            if (!world.TryGetVoxel(source.Key, out _) ||
                source.Value <= world.GetLight(source.Key, Channel.Block))
                continue;

            world.SetLight(source.Key, Channel.Block, source.Value);
            sourcePropagation.Enqueue(source.Key);
        }

        Propagate(sourcePropagation, Channel.Block, false);
    }

    /// <summary>Rebuilds block light after voxel/chunk topology changes.</summary>
    public void RebuildBlockLight()
    {
        world.ClearLight(Channel.Block);
        
        Queue<Vector3Int> propagation = new Queue<Vector3Int>();
        foreach (KeyValuePair<Vector3Int, byte> source in blockLightSources)
        {
            if (!world.TryGetVoxel(source.Key, out _))
                continue;

            world.SetLight(source.Key, Channel.Block, source.Value);
            propagation.Enqueue(source.Key);
        }

        Propagate(propagation, Channel.Block, false);
    }

    private void Propagate(Queue<Vector3Int> queue, Channel channel, bool preserveDownwardSun)
    {
        while (queue.Count > 0)
        {
            Vector3Int position = queue.Dequeue();
            byte level = world.GetLight(position, channel);
            if (level == VoxelLight.Min)
                continue;

            foreach (Vector3Int direction in Directions)
            {
                Vector3Int neighbor = position + direction;
                if (!IsTransparent(neighbor))
                    continue;

                byte nextLevel = preserveDownwardSun && direction == Vector3Int.down && level == VoxelLight.Max
                    ? VoxelLight.Max
                    : (byte)(level - 1);

                if (nextLevel <= world.GetLight(neighbor, channel))
                    continue;

                world.SetLight(neighbor, channel, nextLevel);
                queue.Enqueue(neighbor);
            }
        }
    }

    private bool IsTransparent(Vector3Int position)
    {
        return world.TryGetVoxel(position, out byte blockId) && !VoxelLight.BlocksSkyLight(blockId);
    }
}
