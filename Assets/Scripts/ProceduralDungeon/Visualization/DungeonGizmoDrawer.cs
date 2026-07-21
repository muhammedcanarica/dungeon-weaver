using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator))]
    public sealed class DungeonGizmoDrawer : MonoBehaviour
    {
        private static readonly Color GenerationAreaColor = new Color(0.25f, 0.7f, 1f, 1f);
        private static readonly Color RoomColor = new Color(0.2f, 1f, 0.45f, 1f);
        private static readonly Color CenterColor = new Color(1f, 0.75f, 0.2f, 1f);
        private static readonly Color PrimaryConnectionColor = new Color(0.35f, 0.75f, 1f, 1f);
        private static readonly Color ExtraConnectionColor = new Color(1f, 0.55f, 0.15f, 1f);
        private static readonly Color PrimaryCorridorColor = new Color(0.9f, 0.35f, 0.8f, 1f);
        private static readonly Color ExtraCorridorColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color StartRoomColor = new Color(0.15f, 1f, 0.25f, 0.22f);
        private static readonly Color BossRoomColor = new Color(1f, 0.15f, 0.15f, 0.22f);
        private const float CenterMarkerRadius = 0.15f;

        private void OnDrawGizmos()
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>();

            if (generator == null)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            DrawGenerationArea(generator.GenerationArea);
            DungeonRoomRoleAssigner roles = GetComponent<DungeonRoomRoleAssigner>();
            DrawConnections(generator, GetComponent<DungeonGraphBuilder>());
            DrawCorridors(GetComponent<DungeonCorridorBuilder>());
            DrawRoleHighlights(generator, roles);
            DrawRooms(generator, roles);

            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }

        private static void DrawGenerationArea(RectInt area)
        {
            Gizmos.color = GenerationAreaColor;
            Gizmos.DrawWireCube(
                new Vector3(area.center.x, area.center.y, 0f),
                new Vector3(area.width, area.height, 0f));
        }

        private void DrawRooms(DungeonGenerator generator, DungeonRoomRoleAssigner roles)
        {
            IReadOnlyList<RoomData> rooms = generator.Rooms;

            for (int i = 0; i < rooms.Count; i++)
            {
                RoomData room = rooms[i];
                Vector3 localCenter = new Vector3(room.Center.x, room.Center.y, 0f);

                Gizmos.color = RoomColor;
                Gizmos.DrawWireCube(
                    localCenter,
                    new Vector3(room.Width, room.Height, 0f));

                Gizmos.color = CenterColor;
                Gizmos.DrawSphere(localCenter, CenterMarkerRadius);

#if UNITY_EDITOR
                Vector3 worldLabelPosition = transform.TransformPoint(localCenter);
                string suffix = string.Empty;
                if (roles != null && roles.IsRoleDataCurrent)
                {
                    if (room.RoomId == roles.StartRoomId) suffix = " START";
                    else if (room.RoomId == roles.BossRoomId) suffix = " BOSS";
                }
                Handles.Label(worldLabelPosition, $"Room {room.RoomId}{suffix}");
#endif
            }
        }

        private static void DrawConnections(DungeonGenerator generator, DungeonGraphBuilder graph)
        {
            if (graph == null || !graph.IsGraphCurrent) return;
            for (int i = 0; i < graph.Connections.Count; i++)
            {
                RoomConnectionData edge = graph.Connections[i];
                if (!FindRoom(generator.Rooms, edge.RoomAId, out RoomData a) || !FindRoom(generator.Rooms, edge.RoomBId, out RoomData b)) continue;
                Gizmos.color = edge.IsPrimaryConnection ? PrimaryConnectionColor : ExtraConnectionColor;
                Gizmos.DrawLine(new Vector3(a.Center.x, a.Center.y, 0f), new Vector3(b.Center.x, b.Center.y, 0f));
            }
        }

        private static void DrawCorridors(DungeonCorridorBuilder builder)
        {
            if (builder == null || !builder.IsCorridorDataCurrent) return;
            for (int i = 0; i < builder.Corridors.Count; i++)
            {
                CorridorData corridor = builder.Corridors[i];
                Gizmos.color = corridor.IsPrimaryConnection ? PrimaryCorridorColor : ExtraCorridorColor;
                for (int p = 1; p < corridor.PathCells.Count; p++) Gizmos.DrawLine(CellCenter(corridor.PathCells[p - 1]), CellCenter(corridor.PathCells[p]));
                Gizmos.color = Color.white;
                Gizmos.DrawCube(CellCenter(corridor.StartDoorCell), Vector3.one * 0.3f);
                Gizmos.DrawCube(CellCenter(corridor.EndDoorCell), Vector3.one * 0.3f);
            }
        }

        private static void DrawRoleHighlights(DungeonGenerator generator, DungeonRoomRoleAssigner roles)
        {
            if (roles == null || !roles.IsRoleDataCurrent) return;
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                if (room.RoomId != roles.StartRoomId && room.RoomId != roles.BossRoomId) continue;
                Gizmos.color = room.RoomId == roles.StartRoomId ? StartRoomColor : BossRoomColor;
                Gizmos.DrawCube(new Vector3(room.Center.x, room.Center.y, 0f), new Vector3(room.Width, room.Height, 0.02f));
            }
        }

        private static bool FindRoom(IReadOnlyList<RoomData> rooms, int id, out RoomData match)
        {
            for (int i = 0; i < rooms.Count; i++) if (rooms[i] != null && rooms[i].RoomId == id) { match = rooms[i]; return true; }
            match = null; return false;
        }

        private static Vector3 CellCenter(Vector2Int cell) => new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }
}
