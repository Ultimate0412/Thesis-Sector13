using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sector13.Decals
{
    /// <summary>
    /// คลัง decal: รวม atlas หลายแผ่น + ค้นหาด้วย TraceType
    /// ใช้แทนการอ้าง material 6 ตัวแบบตรง ๆ ใน PackageTraceApplier
    /// </summary>
    [CreateAssetMenu(menuName = "Sector13/Decals/Decal Library", fileName = "DecalLibrary")]
    public class DecalLibrary : ScriptableObject
    {
        public List<DecalAtlas> atlases = new List<DecalAtlas>();

        [Header("Fallback")]
        [Tooltip("ถ้าหาไม่เจอ ให้ใช้ material ตัวนี้ (ปล่อยว่างได้)")]
        public Material fallbackMaterial;

        private readonly Dictionary<TraceType, DecalEntry> lookup = new Dictionary<TraceType, DecalEntry>();
        private bool lookupBuilt;

        private void OnEnable()
        {
            lookupBuilt = false;
        }

        public void RebuildLookup()
        {
            lookup.Clear();
            for (int a = 0; a < atlases.Count; a++)
            {
                var atlas = atlases[a];
                if (atlas == null) continue;
                for (int i = 0; i < atlas.entries.Count; i++)
                {
                    var e = atlas.entries[i];
                    if (e == null) continue;
                    // ชนิดแรกที่เจอชนะ (เรียงตามลำดับ atlas ในลิสต์)
                    if (!lookup.ContainsKey(e.traceType)) lookup.Add(e.traceType, e);
                }
            }
            lookupBuilt = true;
        }

        public bool TryGetEntry(TraceType type, out DecalEntry entry)
        {
            if (!lookupBuilt) RebuildLookup();
            return lookup.TryGetValue(type, out entry);
        }

        /// <summary>หา entry จาก id ที่ระบุตรง ๆ (ใช้เลือก variant เฉพาะ เช่น BloodStain_Down_A)</summary>
        public bool TryGetEntryById(string id, out DecalEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(id)) return false;
            for (int a = 0; a < atlases.Count; a++)
            {
                var atlas = atlases[a];
                if (atlas == null) continue;
                var found = atlas.FindById(id);
                if (found != null)
                {
                    entry = found;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetAtlas(TraceType type, out DecalAtlas atlas, out DecalEntry entry)
        {
            entry = null;
            atlas = null;
            if (!TryGetEntry(type, out entry) || entry == null) return false;

            for (int a = 0; a < atlases.Count; a++)
            {
                var candidate = atlases[a];
                if (candidate == null) continue;
                if (candidate.entries.Contains(entry))
                {
                    atlas = candidate;
                    return true;
                }
            }
            return false;
        }

        public IReadOnlyList<DecalEntry> AllEntries()
        {
            var list = new List<DecalEntry>();
            for (int a = 0; a < atlases.Count; a++)
            {
                if (atlases[a] == null) continue;
                list.AddRange(atlases[a].entries);
            }
            return list;
        }

        /// <summary>
        /// ผูก decal ชนิดที่กำหนดลงบน projector (material + atlas uv + ขนาด)
        /// </summary>
        public bool Apply(TraceType type, DecalProjector projector, float worldWidth, float worldHeight)
        {
            if (projector == null) return false;
            if (!TryGetEntry(type, out var entry) || entry == null)
            {
                if (fallbackMaterial != null) projector.material = fallbackMaterial;
                return false;
            }

            projector.material = entry.material != null ? entry.material : fallbackMaterial;
            projector.uvScale = new Vector2(entry.uvRect.width, entry.uvRect.height);
            projector.uvBias = new Vector2(entry.uvRect.x, entry.uvRect.y);
            if (worldWidth > 0f && worldHeight > 0f)
            {
                projector.size = new Vector3(worldWidth, worldHeight, projector.size.z);
            }
            return true;
        }
    }
}
