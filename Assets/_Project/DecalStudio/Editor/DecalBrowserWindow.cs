using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sector13.Decals.EditorTools
{
    /// <summary>
    /// หน้ารวม decal ทุกช่องในทุก atlas — เห็นรูปจริง (crop จาก atlas × tint) พร้อม id และชนิด
    /// ใช้เลือก variant ที่ต้องการโดยไม่ต้องเปิด material ไล่ทีละอัน
    /// </summary>
    public class DecalBrowserWindow : EditorWindow
    {
        const string LibraryPath = "Assets/_Project/DecalStudio/Art/DecalLibrary.asset";
        const float TileWidth = 124f;
        const float ThumbSize = 100f;

        class Item
        {
            public DecalAtlas atlas;
            public DecalEntry entry;
        }

        readonly List<Item> items = new List<Item>();
        readonly List<Item> filtered = new List<Item>();

        DecalLibrary library;
        string search = string.Empty;
        bool uvOnly;
        int typeFilter;              // 0 = ทุกชนิด, 1.. = TraceType ตามลำดับ enum
        Vector2 scroll;

        [MenuItem("Tools/Sector13/Decal Studio/Decal Browser", false, 13)]
        public static void Open()
        {
            var window = GetWindow<DecalBrowserWindow>("Decal Browser");
            window.minSize = new Vector2(470, 400);
            window.Show();
        }

        void OnEnable()
        {
            Reload();
        }

        void OnFocus()
        {
            Reload();
        }

        void Reload()
        {
            items.Clear();
            library = AssetDatabase.LoadAssetAtPath<DecalLibrary>(LibraryPath);
            if (library == null) return;

            foreach (var atlas in library.atlases)
            {
                if (atlas == null) continue;
                foreach (var entry in atlas.entries)
                {
                    if (entry == null) continue;
                    items.Add(new Item { atlas = atlas, entry = entry });
                }
            }
        }

        void OnGUI()
        {
            BuildFilteredList();
            DrawToolbar();

            if (library == null)
            {
                EditorGUILayout.HelpBox($"ไม่พบ DecalLibrary ที่ {LibraryPath}", MessageType.Warning);
                return;
            }

            BuildFilteredList();
            DrawGrid();

            EditorGUILayout.LabelField("คลิก = เลือก material · คลิกขวา = เมนู (คัดลอก id / ping)", EditorStyles.centeredGreyMiniLabel);
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(search ?? string.Empty, EditorStyles.toolbarSearchField, GUILayout.MinWidth(150));

                var typeNames = new List<string> { "ทุกชนิด" };
                typeNames.AddRange(System.Enum.GetNames(typeof(TraceType)));
                typeFilter = Mathf.Clamp(typeFilter, 0, typeNames.Count - 1);
                typeFilter = EditorGUILayout.Popup(typeFilter, typeNames.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(130));

                uvOnly = GUILayout.Toggle(uvOnly, "เฉพาะ UV", EditorStyles.toolbarButton, GUILayout.Width(74));

                if (GUILayout.Button("โหลดใหม่", EditorStyles.toolbarButton, GUILayout.Width(68))) Reload();

                GUILayout.FlexibleSpace();
                GUILayout.Label($"{filtered.Count}/{items.Count}", EditorStyles.miniLabel, GUILayout.Width(56));
            }
        }

        void BuildFilteredList()
        {
            filtered.Clear();
            string needle = (search ?? string.Empty).Trim();
            TraceType? type = null;
            if (typeFilter > 0)
            {
                var names = System.Enum.GetNames(typeof(TraceType));
                int index = typeFilter - 1;
                if (index < names.Length) type = (TraceType)System.Enum.Parse(typeof(TraceType), names[index]);
            }

            foreach (var item in items)
            {
                var entry = item.entry;
                if (uvOnly && !entry.requiresUV) continue;
                if (type.HasValue && entry.traceType != type.Value) continue;
                if (needle.Length > 0 &&
                    entry.id.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.traceType.ToString().IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                filtered.Add(item);
            }
        }

        void DrawGrid()
        {
            float available = position.width - 26f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(available / (TileWidth + 6f)));

            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.BeginHorizontal();
            int column = 0;

            foreach (var item in filtered)
            {
                DrawTile(item);
                column++;
                if (column % perRow == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }

            GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        void DrawTile(Item item)
        {
            var entry = item.entry;

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(TileWidth)))
            {
                Rect rect = GUILayoutUtility.GetRect(ThumbSize, ThumbSize, GUILayout.Width(ThumbSize), GUILayout.Height(ThumbSize));
                bool hover = rect.Contains(Event.current.mousePosition);
                bool selected = Selection.activeObject == entry.material;

                DecalPreview.Draw(rect, item.atlas != null ? item.atlas.albedo : null, entry.uvRect, entry.tint);

                if (hover) EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), new Color(0.35f, 0.65f, 1f, 1f));
                if (selected) DecalPreview.DrawBorder(rect);

                if (hover)
                {
                    var e = Event.current;
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        Select(entry);
                        e.Use();
                    }
                    else if (e.type == EventType.MouseDown && e.button == 1)
                    {
                        ShowContextMenu(item);
                        e.Use();
                    }
                }

                GUILayout.Label(entry.id, EditorStyles.miniBoldLabel);
                GUILayout.Label(
                    (entry.requiresUV ? "UV · " : string.Empty) + entry.traceType,
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        static void Select(DecalEntry entry)
        {
            if (entry == null || entry.material == null) return;
            Selection.activeObject = entry.material;
            EditorGUIUtility.PingObject(entry.material);
        }

        void ShowContextMenu(Item item)
        {
            var menu = new GenericMenu();
            var entry = item.entry;

            menu.AddItem(new GUIContent("เลือก material"), false, () => Select(entry));
            menu.AddItem(new GUIContent("คัดลอก id"), false, () => EditorGUIUtility.systemCopyBuffer = entry.id);

            if (item.atlas != null)
            {
                menu.AddItem(new GUIContent("Ping atlas"), false, () =>
                {
                    Selection.activeObject = item.atlas;
                    EditorGUIUtility.PingObject(item.atlas);
                });
            }

            if (entry.material != null)
            {
                menu.AddItem(new GUIContent("คัดลอก path material"), false, () =>
                    EditorGUIUtility.systemCopyBuffer = AssetDatabase.GetAssetPath(entry.material));
            }

            menu.ShowAsContext();
        }
    }
}
