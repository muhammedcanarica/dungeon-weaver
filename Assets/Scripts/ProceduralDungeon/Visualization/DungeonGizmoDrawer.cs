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
            DrawRooms(generator);

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

        private void DrawRooms(DungeonGenerator generator)
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
                Handles.Label(worldLabelPosition, $"Room {room.RoomId}");
#endif
            }
        }
    }
}
