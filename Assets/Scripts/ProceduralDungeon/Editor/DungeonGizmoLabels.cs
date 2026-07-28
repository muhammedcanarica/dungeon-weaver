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
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                string suffix = string.Empty;
                if (roles != null && roles.IsRoleDataCurrent)
                {
                    if (room.RoomId == roles.StartRoomId) suffix = " START";
                    else if (room.RoomId == roles.BossRoomId) suffix = " BOSS";
                }
                Vector3 localCenter = new Vector3(room.Center.x, room.Center.y, 0f);
                Handles.Label(drawer.transform.TransformPoint(localCenter),
                    $"Room {room.RoomId}{suffix}");
            }
        }
    }
}
