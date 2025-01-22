using UnityEngine;
using UnityEditor;
using System.Linq;

public class EditorTools : EditorWindow
{

    [MenuItem("MakiGriever/Layout objects in grid layout")]
    private static void LayoutObjects()
    {
        // Get selected objects and sort them alphabetically
        GameObject[] selectedObjects = Selection.gameObjects
            .OrderBy(go => go.name)
            .ToArray();

        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "Please select some GameObjects first!", "OK");
            return;
        }

        // Calculate grid dimensions
        int totalObjects = selectedObjects.Length;
        float sqrt = Mathf.Sqrt(totalObjects);
        int columns = Mathf.CeilToInt(sqrt);
        int rows = Mathf.CeilToInt(totalObjects / (float)columns);

        // Define grid spacing
        float spacingX = 50.0f;  // Space between objects horizontally
        float spacingZ = 50.0f;  // Space between objects vertically

        // Calculate starting position to center the grid
        Vector3 startPos = new Vector3(
            -(columns * spacingX) / 2f,
            0f,
            -(rows * spacingZ) / 2f
        );

        // Create undo group
        Undo.RecordObjects(selectedObjects, "Layout Objects in Grid");

        // Place objects in grid
        for (int i = 0; i < totalObjects; i++)
        {
            int row = i / columns;
            int col = i % columns;

            Vector3 newPosition = startPos + new Vector3(
                col * spacingX,
                0f,
                row * spacingZ
            );

            // Set position and reset rotation
            selectedObjects[i].transform.position = newPosition;
            selectedObjects[i].transform.rotation = Quaternion.identity;
        }
    }
}