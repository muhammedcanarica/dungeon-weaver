using UnityEditor;
using UnityEngine;

namespace ProceduralDungeon.Editor
{
    internal static class DungeonGizmoLabels
    {
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawRoomLabels(DungeonGizmoDrawer drawer, GizmoType gizmoType)
        {
            DungeonGenerator generator = drawer.GetComponent<DungeonGenerator>();
            if (generator == null) return;
            DungeonRoomRoleAssigner roles = drawer.GetComponent<DungeonRoomRoleAssigner>();
            DungeonGraphBuilder graph = drawer.GetComponent<DungeonGraphBuilder>();
            DungeonGraphAnalysis topology = graph != null && graph.IsGraphCurrent
                ? graph.Analysis : null;
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                string suffix = string.Empty;
                if (roles != null && roles.IsRoleDataCurrent)
                {
                    if (room.RoomId == roles.StartRoomId) suffix = " START";
                    else if (room.RoomId == roles.BossRoomId) suffix = " BOSS";
                }
                string topologyLabel = string.Empty;
                if (topology != null)
                {
                    if (topology.IsOnCriticalPath(room.RoomId)) topologyLabel = "CP";
                    if (topology.IsDeadEnd(room.RoomId))
                        topologyLabel += topologyLabel.Length > 0 ? " · DEAD END" : "DEAD END";
                    else if (topology.IsJunction(room.RoomId))
                        topologyLabel += topologyLabel.Length > 0 ? " · JUNCTION" : "JUNCTION";
                }
                Vector3 localCenter = new Vector3(room.Center.x, room.Center.y, 0f);
                Handles.Label(drawer.transform.TransformPoint(localCenter),
                    $"Room {room.RoomId}{suffix}\n{room.ShapeType} / {room.Archetype}"
                    + (topologyLabel.Length > 0 ? $"\n{topologyLabel}" : string.Empty));
            }
        }
    }
}
