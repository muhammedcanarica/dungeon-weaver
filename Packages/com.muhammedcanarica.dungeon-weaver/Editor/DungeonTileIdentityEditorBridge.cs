using UnityEditor;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon.Editor
{
    [InitializeOnLoad]
    internal static class DungeonTileIdentityEditorBridge
    {
        static DungeonTileIdentityEditorBridge()
        {
            DungeonTilemapRenderer.SetTileIdentityResolver(ResolveGuid);
        }

        private static string ResolveGuid(TileBase tile)
        {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile));
        }
    }
}
