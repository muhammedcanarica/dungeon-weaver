using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonGraphBuilder), typeof(DungeonCorridorBuilder))]
    public sealed class DungeonTilemapRenderer : MonoBehaviour
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private static readonly Vector2Int[] Neighbors = {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down,
            new Vector2Int(-1,1), new Vector2Int(1,1), new Vector2Int(-1,-1), new Vector2Int(1,-1)
        };
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private TileBase floorTile;
        [SerializeField] private TileBase wallTile;
        [SerializeField, Min(1)] private int corridorWidth = 3;
        [SerializeField, Min(1)] private int wallThickness = 1;
        [SerializeField] private bool clearBeforeRender = true;
        [SerializeField, HideInInspector] private int floorCellCount;
        [SerializeField, HideInInspector] private int wallCellCount;
        [SerializeField, HideInInspector] private long builtRenderSignature;
        [SerializeField, HideInInspector] private bool hasRenderedTilemap;
        [SerializeField, HideInInspector] private string builtFloorTileIdentity = string.Empty;
        [SerializeField, HideInInspector] private string builtWallTileIdentity = string.Empty;

        public int FloorCellCount => floorCellCount;
        public int WallCellCount => wallCellCount;
        public bool IsTilemapCurrent
        {
            get
            {
                DungeonGenerator generator = GetComponent<DungeonGenerator>();
                DungeonGraphBuilder graph = GetComponent<DungeonGraphBuilder>();
                DungeonCorridorBuilder corridors = GetComponent<DungeonCorridorBuilder>();
                if (generator == null || graph == null || corridors == null || !corridors.IsCorridorDataCurrent || !hasRenderedTilemap
                    || floorTilemap == null || wallTilemap == null || floorTile == null || wallTile == null || floorCellCount <= 0) return false;
                string floorId = TileId(floorTile, builtFloorTileIdentity), wallId = TileId(wallTile, builtWallTileIdentity);
                return floorId == builtFloorTileIdentity && wallId == builtWallTileIdentity
                    && builtRenderSignature == Signature(generator.Rooms, graph.Connections, corridors.Corridors, corridorWidth, wallThickness, floorId, wallId)
                    && Count(floorTilemap) == floorCellCount && Count(wallTilemap) == wallCellCount;
            }
        }

        [ContextMenu("Render Dungeon Tilemap")]
        public void RenderDungeonTilemap()
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>(); DungeonGraphBuilder graph = GetComponent<DungeonGraphBuilder>(); DungeonCorridorBuilder corridors = GetComponent<DungeonCorridorBuilder>();
            if (!ValidateInputs(generator, graph, corridors, out string error)) { Debug.LogWarning($"[DungeonTilemapRenderer] Render rejected: {error}", this); return; }
            if (clearBeforeRender) { floorTilemap.ClearAllTiles(); wallTilemap.ClearAllTiles(); }
            List<Vector2Int> floors = Floors(generator.Rooms, corridors.Corridors, corridorWidth); List<Vector2Int> walls = Walls(floors, wallThickness);
            Set(floorTilemap, floors, floorTile); Set(wallTilemap, walls, wallTile); floorTilemap.CompressBounds(); wallTilemap.CompressBounds(); RefreshCollider();
            floorCellCount = floors.Count; wallCellCount = walls.Count; builtFloorTileIdentity = TileId(floorTile, floorTile.name); builtWallTileIdentity = TileId(wallTile, wallTile.name);
            builtRenderSignature = Signature(generator.Rooms, graph.Connections, corridors.Corridors, corridorWidth, wallThickness, builtFloorTileIdentity, builtWallTileIdentity); hasRenderedTilemap = true;
            Debug.Log($"[DungeonTilemapRenderer] Rendered {floorCellCount} floor cells and {wallCellCount} wall cells.", this);
        }

        [ContextMenu("Clear Dungeon Tilemap")]
        public void ClearDungeonTilemap()
        {
            if (floorTilemap != null) floorTilemap.ClearAllTiles(); if (wallTilemap != null) wallTilemap.ClearAllTiles(); RefreshCollider();
            floorCellCount = 0; wallCellCount = 0; builtRenderSignature = 0; hasRenderedTilemap = false; builtFloorTileIdentity = string.Empty; builtWallTileIdentity = string.Empty;
        }

        private bool ValidateInputs(DungeonGenerator generator, DungeonGraphBuilder graph, DungeonCorridorBuilder corridors, out string error)
        {
            if (generator == null || graph == null || corridors == null || !corridors.IsCorridorDataCurrent) { error = "Corridor data is stale."; return false; }
            if (floorTilemap == null || wallTilemap == null || floorTilemap == wallTilemap || floorTile == null || wallTile == null) { error = "Tilemap or Tile references are invalid."; return false; }
            if (corridorWidth <= 0 || (corridorWidth & 1) == 0) { error = "Corridor Width must be a positive odd number."; return false; }
            if (wallThickness <= 0) { error = "Wall Thickness must be positive."; return false; }
            if (!Identity(transform)) { error = "DungeonGenerator transform must be identity at world origin."; return false; }
            Grid grid = floorTilemap.GetComponentInParent<Grid>();
            if (grid == null || wallTilemap.GetComponentInParent<Grid>() != grid || !Identity(grid.transform) || grid.cellSize != new Vector3(1,1,0)) { error = "Tilemaps must share an origin Grid with cell size (1,1,0)."; return false; }
            error = string.Empty; return true;
        }
        private static bool Identity(Transform t) => t.position.sqrMagnitude < 0.00000001f && Quaternion.Angle(t.rotation, Quaternion.identity) < 0.0001f && (t.lossyScale - Vector3.one).sqrMagnitude < 0.00000001f;
        private static List<Vector2Int> Floors(IReadOnlyList<RoomData> rooms, IReadOnlyList<CorridorData> corridors, int width)
        {
            var set = new HashSet<Vector2Int>();
            for (int i = 0; i < rooms.Count; i++) for (int y = rooms[i].Bounds.yMin; y < rooms[i].Bounds.yMax; y++) for (int x = rooms[i].Bounds.xMin; x < rooms[i].Bounds.xMax; x++) set.Add(new Vector2Int(x,y));
            int radius = width / 2;
            for (int i = 0; i < corridors.Count; i++) for (int p = 0; p < corridors[i].PathCells.Count; p++)
                for (int y = corridors[i].PathCells[p].y - radius; y <= corridors[i].PathCells[p].y + radius; y++)
                    for (int x = corridors[i].PathCells[p].x - radius; x <= corridors[i].PathCells[p].x + radius; x++) set.Add(new Vector2Int(x,y));
            return Sort(set);
        }
        private static List<Vector2Int> Walls(IReadOnlyList<Vector2Int> floors, int thickness)
        {
            var floorSet = new HashSet<Vector2Int>(floors); var walls = new HashSet<Vector2Int>(); var frontier = new HashSet<Vector2Int>(floorSet);
            for (int layer = 0; layer < thickness; layer++)
            {
                var next = new HashSet<Vector2Int>(); foreach (Vector2Int cell in frontier) for (int i = 0; i < Neighbors.Length; i++) { Vector2Int candidate = cell + Neighbors[i]; if (!floorSet.Contains(candidate) && walls.Add(candidate)) next.Add(candidate); }
                frontier = next;
            }
            return Sort(walls);
        }
        private static List<Vector2Int> Sort(IEnumerable<Vector2Int> cells) { var list = new List<Vector2Int>(cells); list.Sort((a,b) => { int y = a.y.CompareTo(b.y); return y != 0 ? y : a.x.CompareTo(b.x); }); return list; }
        private static void Set(Tilemap map, IReadOnlyList<Vector2Int> cells, TileBase tile)
        {
            var positions = new Vector3Int[cells.Count]; var tiles = new TileBase[cells.Count];
            for (int i = 0; i < cells.Count; i++) { positions[i] = new Vector3Int(cells[i].x, cells[i].y, 0); tiles[i] = tile; }
            map.SetTiles(positions, tiles);
        }
        private void RefreshCollider() { if (wallTilemap == null) return; TilemapCollider2D collider = wallTilemap.GetComponent<TilemapCollider2D>(); if (collider == null) return; collider.ProcessTilemapChanges(); CompositeCollider2D composite = wallTilemap.GetComponent<CompositeCollider2D>(); if (composite != null) composite.GenerateGeometry(); }
        private static int Count(Tilemap map) { int count = 0; foreach (Vector3Int p in map.cellBounds.allPositionsWithin) if (map.HasTile(p)) count++; return count; }
        private static long Signature(IReadOnlyList<RoomData> rooms, IReadOnlyList<RoomConnectionData> edges, IReadOnlyList<CorridorData> corridors, int width, int thickness, string floorId, string wallId)
        {
            ulong hash = Offset;
            for (int i=0;i<rooms.Count;i++){RoomData r=rooms[i];AddInt(ref hash,r.RoomId);AddInt(ref hash,r.Bounds.x);AddInt(ref hash,r.Bounds.y);AddInt(ref hash,r.Bounds.width);AddInt(ref hash,r.Bounds.height);}
            for(int i=0;i<edges.Count;i++){RoomConnectionData e=edges[i];AddInt(ref hash,e.RoomAId);AddInt(ref hash,e.RoomBId);AddLong(ref hash,e.WeightSquared);AddByte(ref hash,e.IsPrimaryConnection?(byte)1:(byte)0);}
            for(int i=0;i<corridors.Count;i++){CorridorData c=corridors[i];AddInt(ref hash,c.RoomAId);AddInt(ref hash,c.RoomBId);AddByte(ref hash,c.IsPrimaryConnection?(byte)1:(byte)0);AddByte(ref hash,c.UsedFallbackPathfinding?(byte)1:(byte)0);AddInt(ref hash,c.CellCount);for(int p=0;p<c.PathCells.Count;p++){AddInt(ref hash,c.PathCells[p].x);AddInt(ref hash,c.PathCells[p].y);}}
            AddInt(ref hash,width);AddInt(ref hash,thickness);AddString(ref hash,floorId);AddString(ref hash,wallId);return unchecked((long)hash);
        }
        private static string TileId(TileBase tile,string fallback){if(tile==null)return string.Empty;
#if UNITY_EDITOR
            string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile));return string.IsNullOrEmpty(guid)?tile.name:guid;
#else
            return string.IsNullOrEmpty(fallback)?tile.name:fallback;
#endif
        }
        private static void AddString(ref ulong h,string v){if(v==null){AddInt(ref h,-1);return;}AddInt(ref h,v.Length);for(int i=0;i<v.Length;i++)AddInt(ref h,v[i]);}
        private static void AddInt(ref ulong h,int v){uint b=unchecked((uint)v);for(int s=0;s<32;s+=8)AddByte(ref h,(byte)(b>>s));}
        private static void AddLong(ref ulong h,long v){ulong b=unchecked((ulong)v);for(int s=0;s<64;s+=8)AddByte(ref h,(byte)(b>>s));}
        private static void AddByte(ref ulong h,byte v){h^=v;h=unchecked(h*Prime);}
    }
}
