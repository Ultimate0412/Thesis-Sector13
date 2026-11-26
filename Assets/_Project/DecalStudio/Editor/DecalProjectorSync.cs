using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// ตัวช่วยให้ UV ของ Decal Projector ตรงกับ cell ที่ material ใช้
    /// (material เก็บ cell ไว้ใน _DecalRect / DecalLibrary)
    /// </summary>
    public static class DecalProjectorSync
    {
        public const float UvInset = 0.004f;

        public static bool Sync(DecalProjector projector, bool undo = true)
        {
            if (projector == null) return false;

            var material = projector.material;
            if (material == null) return false;

            Rect cell = DecalPreview.GetCell(material);
            if (cell.width <= 0.0001f || cell.height <= 0.0001f) return false;
            if (cell.x <= 0.0001f && cell.y <= 0.0001f && cell.width >= 0.9999f && cell.height >= 0.9999f)
                return false; // full atlas = ไม่มี cell ให้ sync

            if (undo) Undo.RecordObject(projector, "Sync Decal UV");
            projector.uvScale = new Vector2(cell.width - UvInset * 2f, cell.height - UvInset * 2f);
            projector.uvBias = new Vector2(cell.x + UvInset, cell.y + UvInset);
            EditorUtility.SetDirty(projector);
            return true;
        }

        public static int SyncAll()
        {
            var projectors = Object.FindObjectsByType<DecalProjector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;
            for (int i = 0; i < projectors.Length; i++)
                if (Sync(projectors[i])) changed++;
            return changed;
        }

        // คลิกขวาที่หัว component Decal Projector → Sync UV from Material
        [MenuItem("CONTEXT/DecalProjector/Sync UV from Material")]
        static void SyncContext(MenuCommand command)
        {
            Sync(command.context as DecalProjector);
        }

        [MenuItem("Tools/Sector13/Decal Studio/Sync All Projector UVs")]
        static void SyncAllMenu()
        {
            int changed = SyncAll();
            Debug.Log($"[DecalProjectorSync] sync {changed} projector(s)");
        }
    }
}
