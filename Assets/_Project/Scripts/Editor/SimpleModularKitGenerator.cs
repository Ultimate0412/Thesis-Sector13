using UnityEngine;
using UnityEditor;

namespace ThesisSector13.Editor
{
    /// <summary>
    /// Simple Modular Kit Generator using Unity Primitives (Cube)
    /// </summary>
    public class SimpleModularKitGenerator : EditorWindow
    {
        private float gridSpacing = 0.5f;
        private float categorySpacing = 8f;

        [MenuItem("Thesis-Sector13/Generate Simple Modular Kit")]
        public static void ShowWindow()
        {
            GetWindow<SimpleModularKitGenerator>("Simple Modular Kit Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Simple Modular Kit Generator", EditorStyles.boldLabel);
            GUILayout.Label("Uses Unity Primitives (Cubes)", EditorStyles.miniLabel);
            GUILayout.Space(10);

            gridSpacing = EditorGUILayout.FloatField("Grid Spacing", gridSpacing);
            categorySpacing = EditorGUILayout.FloatField("Category Spacing", categorySpacing);

            GUILayout.Space(20);

            if (GUILayout.Button("Generate All Modular Pieces", GUILayout.Height(40)))
            {
                GenerateModularKit();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Clear All Generated Pieces", GUILayout.Height(30)))
            {
                ClearGeneratedPieces();
            }
        }

        private void GenerateModularKit()
        {
            GameObject root = new GameObject("_ModularKit_Display");
            Undo.RegisterCreatedObjectUndo(root, "Generate Modular Kit");

            float currentX = 0f;

            currentX = GenerateFloorCategory(root.transform, currentX);
            currentX += categorySpacing;

            currentX = GenerateWallCategory(root.transform, currentX);
            currentX += categorySpacing;

            currentX = GenerateCeilingCategory(root.transform, currentX);
            currentX += categorySpacing;

            currentX = GenerateCorridorCategory(root.transform, currentX);
            currentX += categorySpacing;

            currentX = GenerateDoorCategory(root.transform, currentX);
            currentX += categorySpacing;

            currentX = GeneratePropsCategory(root.transform, currentX);

            Selection.activeGameObject = root;
            Debug.Log("Modular Kit generated successfully!");
        }

        private float GenerateFloorCategory(Transform parent, float startX)
        {
            GameObject category = new GameObject("FLOOR");
            category.transform.parent = parent;
            category.transform.position = new Vector3(startX, 0, 0);

            float currentX = 0f;
            float currentZ = 0f;

            CreateCube(category.transform, "Floor_1x1", 1f, 0.1f, 1f, new Color(0.3f, 0.3f, 0.3f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Floor_2x2", 2f, 0.1f, 2f, new Color(0.3f, 0.3f, 0.3f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Floor_4x4", 4f, 0.1f, 4f, new Color(0.3f, 0.3f, 0.3f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Floor_Corridor_2x4", 2f, 0.1f, 4f, new Color(0.3f, 0.3f, 0.3f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Floor_Corridor_3x4", 3f, 0.1f, 4f, new Color(0.3f, 0.3f, 0.3f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Floor_Room_6x6", 6f, 0.1f, 6f, new Color(0.3f, 0.3f, 0.3f), ref currentX, ref currentZ);

            return startX + currentX + 2f;
        }

        private float GenerateWallCategory(Transform parent, float startX)
        {
            GameObject category = new GameObject("WALL");
            category.transform.parent = parent;
            category.transform.position = new Vector3(startX, 0, 0);

            float currentX = 0f;
            float currentZ = 0f;

            CreateCube(category.transform, "Wall_Standard", 4f, 3f, 0.2f, new Color(0.4f, 0.4f, 0.5f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Wall_Short", 4f, 2.5f, 0.2f, new Color(0.4f, 0.4f, 0.5f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Wall_Tall", 4f, 4f, 0.2f, new Color(0.4f, 0.4f, 0.5f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Wall_Pillar", 0.5f, 3f, 0.5f, new Color(0.35f, 0.35f, 0.4f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Wall_Half", 4f, 1.5f, 0.2f, new Color(0.4f, 0.4f, 0.5f), ref currentX, ref currentZ);

            return startX + currentX + 2f;
        }

        private float GenerateCeilingCategory(Transform parent, float startX)
        {
            GameObject category = new GameObject("CEILING");
            category.transform.parent = parent;
            category.transform.position = new Vector3(startX, 0, 0);

            float currentX = 0f;
            float currentZ = 0f;

            CreateCube(category.transform, "Ceiling_Standard", 4f, 0.15f, 4f, new Color(0.5f, 0.5f, 0.5f), ref currentX, ref currentZ, 3f);
            CreateCube(category.transform, "Ceiling_Low", 4f, 0.15f, 4f, new Color(0.45f, 0.45f, 0.45f), ref currentX, ref currentZ, 2.8f);
            CreateCube(category.transform, "Ceiling_High", 4f, 0.15f, 4f, new Color(0.55f, 0.55f, 0.55f), ref currentX, ref currentZ, 4f);

            return startX + currentX + 2f;
        }

        private float GenerateCorridorCategory(Transform parent, float startX)
        {
            GameObject category = new GameObject("CORRIDOR");
            category.transform.parent = parent;
            category.transform.position = new Vector3(startX, 0, 0);

            float currentX = 0f;
            float currentZ = 0f;

            CreateCorridor(category.transform, "Corridor_Straight_2m", 2f, 2.8f, 4f, ref currentX, ref currentZ);
            CreateCorridor(category.transform, "Corridor_Straight_3m", 3f, 3f, 4f, ref currentX, ref currentZ);
            CreateCorridor(category.transform, "Corridor_Corner_L", 3f, 3f, 4f, ref currentX, ref currentZ);
            CreateCorridor(category.transform, "Corridor_Junction_T", 3f, 3f, 4f, ref currentX, ref currentZ);
            CreateCorridor(category.transform, "Corridor_DeadEnd", 3f, 3f, 2f, ref currentX, ref currentZ);

            return startX + currentX + 2f;
        }

        private float GenerateDoorCategory(Transform parent, float startX)
        {
            GameObject category = new GameObject("DOOR");
            category.transform.parent = parent;
            category.transform.position = new Vector3(startX, 0, 0);

            float currentX = 0f;
            float currentZ = 0f;

            CreateDoor(category.transform, "Door_Standard", 1.2f, 2.4f, 0.2f, ref currentX, ref currentZ);
            CreateDoor(category.transform, "Door_Sliding", 2f, 2.5f, 0.15f, ref currentX, ref currentZ);
            CreateDoor(category.transform, "Door_Airlock", 2.5f, 3f, 0.4f, ref currentX, ref currentZ);
            CreateDoor(category.transform, "Door_Bulkhead", 3f, 3.5f, 0.5f, ref currentX, ref currentZ);

            return startX + currentX + 2f;
        }

        private float GeneratePropsCategory(Transform parent, float startX)
        {
            GameObject category = new GameObject("PROPS");
            category.transform.parent = parent;
            category.transform.position = new Vector3(startX, 0, 0);

            float currentX = 0f;
            float currentZ = 0f;

            CreateCube(category.transform, "Console_Small", 1f, 1.2f, 0.6f, new Color(0.6f, 0.5f, 0.4f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Console_Large", 2f, 1.5f, 0.8f, new Color(0.6f, 0.5f, 0.4f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Crate_Standard", 1f, 1f, 1f, new Color(0.5f, 0.4f, 0.3f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Crate_Small", 0.5f, 0.5f, 0.5f, new Color(0.5f, 0.4f, 0.3f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Pipe_Horizontal", 4f, 0.3f, 0.3f, new Color(0.4f, 0.4f, 0.45f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Pipe_Vertical", 0.3f, 3f, 0.3f, new Color(0.4f, 0.4f, 0.45f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Vent_Grate", 1f, 1f, 0.1f, new Color(0.35f, 0.35f, 0.4f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Panel_Wall", 2f, 1f, 0.1f, new Color(0.45f, 0.45f, 0.5f), ref currentX, ref currentZ);
            CreateCube(category.transform, "Light_Bar", 2f, 0.2f, 0.2f, new Color(0.8f, 0.8f, 0.9f), ref currentX, ref currentZ);

            return startX + currentX + 2f;
        }

        private void CreateCube(Transform parent, string name, float width, float height, float depth, Color color, ref float currentX, ref float currentZ, float yOffset = 0f)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.parent = parent;
            cube.transform.localScale = new Vector3(width, height, depth);
            cube.transform.position = new Vector3(currentX + width / 2f, yOffset + height / 2f, currentZ);

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null)
                mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            cube.GetComponent<MeshRenderer>().sharedMaterial = mat;

            currentX += width + gridSpacing;
        }

        private void CreateCorridor(Transform parent, string name, float width, float height, float length, ref float currentX, ref float currentZ)
        {
            GameObject corridor = new GameObject(name);
            corridor.transform.parent = parent;
            corridor.transform.position = new Vector3(currentX, 0, currentZ);

            // Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = corridor.transform;
            floor.transform.localScale = new Vector3(width, 0.1f, length);
            floor.transform.localPosition = new Vector3(width / 2f, 0.05f, length / 2f);
            Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (floorMat == null) floorMat = new Material(Shader.Find("Standard"));
            floorMat.color = new Color(0.3f, 0.3f, 0.3f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // Left Wall
            GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = "LeftWall";
            leftWall.transform.parent = corridor.transform;
            leftWall.transform.localScale = new Vector3(0.2f, height, length);
            leftWall.transform.localPosition = new Vector3(0.1f, height / 2f, length / 2f);
            Material wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (wallMat == null) wallMat = new Material(Shader.Find("Standard"));
            wallMat.color = new Color(0.4f, 0.4f, 0.5f);
            leftWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Right Wall
            GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "RightWall";
            rightWall.transform.parent = corridor.transform;
            rightWall.transform.localScale = new Vector3(0.2f, height, length);
            rightWall.transform.localPosition = new Vector3(width - 0.1f, height / 2f, length / 2f);
            rightWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Ceiling
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.parent = corridor.transform;
            ceiling.transform.localScale = new Vector3(width, 0.15f, length);
            ceiling.transform.localPosition = new Vector3(width / 2f, height + 0.075f, length / 2f);
            Material ceilingMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (ceilingMat == null) ceilingMat = new Material(Shader.Find("Standard"));
            ceilingMat.color = new Color(0.5f, 0.5f, 0.5f);
            ceiling.GetComponent<MeshRenderer>().sharedMaterial = ceilingMat;

            currentX += width + gridSpacing;
        }

        private void CreateDoor(Transform parent, string name, float width, float height, float depth, ref float currentX, ref float currentZ)
        {
            GameObject door = new GameObject(name);
            door.transform.parent = parent;
            door.transform.position = new Vector3(currentX, 0, currentZ);

            // Frame
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            frame.transform.parent = door.transform;
            frame.transform.localScale = new Vector3(width + 0.3f, height + 0.2f, depth);
            frame.transform.localPosition = new Vector3((width + 0.3f) / 2f, (height + 0.2f) / 2f, 0);
            Material frameMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (frameMat == null) frameMat = new Material(Shader.Find("Standard"));
            frameMat.color = new Color(0.3f, 0.3f, 0.35f);
            frame.GetComponent<MeshRenderer>().sharedMaterial = frameMat;

            // Door
            GameObject doorMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorMesh.name = "Door";
            doorMesh.transform.parent = door.transform;
            doorMesh.transform.localScale = new Vector3(width, height, depth);
            doorMesh.transform.localPosition = new Vector3(width / 2f, height / 2f, 0);
            Material doorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (doorMat == null) doorMat = new Material(Shader.Find("Standard"));
            doorMat.color = new Color(0.5f, 0.5f, 0.55f);
            doorMesh.GetComponent<MeshRenderer>().sharedMaterial = doorMat;

            currentX += width + 0.3f + gridSpacing;
        }

        private void ClearGeneratedPieces()
        {
            GameObject root = GameObject.Find("_ModularKit_Display");
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
                Debug.Log("Cleared all generated pieces.");
            }
            else
            {
                Debug.LogWarning("No generated pieces found.");
            }
        }
    }
}
