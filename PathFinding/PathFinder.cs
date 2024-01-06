using GameOffsets.Native;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Party_Plugin.PathFinding;
public class BinaryHeap<TKey, TValue>
{
    private readonly List<KeyValuePair<TKey, TValue>> _storage = new List<KeyValuePair<TKey, TValue>>();

    private void SieveUp(int startIndex)
    {
        var index = startIndex;
        var nextIndex = (index - 1) / 2;
        while (index != nextIndex)
        {
            if (Compare(index, nextIndex) < 0)
            {
                Swap(index, nextIndex);
            }
            else
            {
                return;
            }

            index = nextIndex;
            nextIndex = (index - 1) / 2;
        }
    }

    private void SieveDown(int startIndex)
    {
        var index = startIndex;
        while (index * 2 + 1 < _storage.Count)
        {
            var child1 = index * 2 + 1;
            var child2 = index * 2 + 2;
            int nextIndex;
            if (child2 < _storage.Count)
            {
                nextIndex = Compare(index, child1) > 0
                                ? Compare(index, child2) > 0
                                      ? Compare(child1, child2) > 0
                                            ? child2
                                            : child1
                                      : child1
                                : Compare(index, child2) > 0
                                    ? child2
                                    : index;
            }
            else
            {
                nextIndex = Compare(index, child1) > 0
                                ? child1
                                : index;
            }

            if (nextIndex == index)
            {
                return;
            }

            Swap(index, nextIndex);
            index = nextIndex;
        }
    }

    private int Compare(int i1, int i2)
    {
        return Comparer<TKey>.Default.Compare(_storage[i1].Key, _storage[i2].Key);
    }

    private void Swap(int i1, int i2)
    {
        (_storage[i1], _storage[i2]) = (_storage[i2], _storage[i1]);
    }

    public void Add(TKey key, TValue value)
    {
        _storage.Add(new KeyValuePair<TKey, TValue>(key, value));
        SieveUp(_storage.Count - 1);
    }

    public bool TryRemoveTop(out KeyValuePair<TKey, TValue> value)
    {
        if (_storage.Count == 0)
        {
            value = default;
            return false;
        }

        value = _storage[0];
        _storage[0] = _storage[^1];
        _storage.RemoveAt(_storage.Count - 1);
        SieveDown(0);
        return true;
    }
}
public class PathFinder
{
    private readonly object _lock = new object();
    private bool[][] _grid;
    private int _dimension1;
    private int _dimension2;

    public PathFinder(int[][] grid, int[] pathableValues)
    {
        UpdateGrid(grid, pathableValues);
    }

    public void UpdateGrid(int[][] grid, int[] pathableValues)
    {
        lock (_lock)
        {
            var pv = pathableValues.ToHashSet();
            _grid = grid.Select(x => x.Select(y => pv.Contains(y)).ToArray()).ToArray();
            _dimension1 = _grid.Length;
            _dimension2 = _grid[0].Length;
        }
    }

    private bool IsTilePathable(Vector2i tile)
    {
        if (tile.X < 0 || tile.X >= _dimension2 || tile.Y < 0 || tile.Y >= _dimension1)
            return false;

        return _grid[tile.Y][tile.X];
    }

    private static readonly Vector2i[] NeighborOffsets = {
        new Vector2i(0, 1), new Vector2i(1, 0), new Vector2i(0, -1), new Vector2i(-1, 0),
        new Vector2i(1, 1), new Vector2i(1, -1), new Vector2i(-1, -1), new Vector2i(-1, 1)
    };

    private IEnumerable<Vector2i> GetNeighbors(Vector2i node)
    {
        foreach (var offset in NeighborOffsets)
        {
            var neighbor = node + offset;
            if (IsTilePathable(neighbor))
            {
                yield return neighbor;
            }
        }
    }

    public List<Vector2i> FindPath(Vector2i start, Vector2i goal)
    {
        var openSet = new HashSet<Vector2i> { start };
        var cameFrom = new Dictionary<Vector2i, Vector2i>();
        var gScore = new Dictionary<Vector2i, float> { [start] = 0 };
        var fScore = new Dictionary<Vector2i, float> { [start] = HeuristicCostEstimate(start, goal) };

        while (openSet.Count > 0)
        {
            var current = openSet.Aggregate((l, r) => fScore[l] < fScore[r] ? l : r);
            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);

            foreach (var neighbor in GetNeighbors(current))
            {
                float tentativeGScore = gScore[current] + Distance(current, neighbor);
                if (tentativeGScore < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + HeuristicCostEstimate(neighbor, goal);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return new List<Vector2i>(); // No path found
    }

    private float HeuristicCostEstimate(Vector2i a, Vector2i b)
    {
        // Manhattan distance
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private float Distance(Vector2i a, Vector2i b)
    {
        // Euclidean distance
        return (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    }

    private List<Vector2i> ReconstructPath(Dictionary<Vector2i, Vector2i> cameFrom, Vector2i current)
    {
        var totalPath = new List<Vector2i> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Insert(0, current);
        }
        return totalPath;
    }
}
