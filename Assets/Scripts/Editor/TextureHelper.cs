using UnityEngine;
using UnityEditor;
using System.Linq;

public class MaterialReplacementTool : EditorWindow
{
    private string materialNameToReplace = "Material_20_ocean";
    private Material destinationMaterial;

    [MenuItem("MakiGriever/Replace material by name")]
    private static void ReplaceOceanMaterials()
    {
        var window = GetWindow<MaterialReplacementTool>();
        window.titleContent = new GUIContent("Material Replacer");
        window.Show();
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Material Replacement Settings", EditorStyles.boldLabel);
        
        // Material name input field
        materialNameToReplace = EditorGUILayout.TextField("Material Name Contains:", materialNameToReplace);
        
        // Destination material object field
        destinationMaterial = (Material)EditorGUILayout.ObjectField(
            "Replacement Material:", 
            destinationMaterial, 
            typeof(Material), 
            false
        );

        // Add some space
        EditorGUILayout.Space(10);

        // Replace button
        if (GUILayout.Button("Replace Materials"))
        {
            ReplaceMaterials();
        }
    }

    private void ReplaceMaterials()
    {
        if (string.IsNullOrEmpty(materialNameToReplace))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a material name to search for!", "OK");
            return;
        }

        if (destinationMaterial == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a replacement material!", "OK");
            return;
        }

        var selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "Please select at least one object in the scene!", "OK");
            return;
        }

        var allMeshRenderers = selectedObjects
            .SelectMany(obj => obj.GetComponentsInChildren<MeshRenderer>(true))
            .ToArray();

        if (allMeshRenderers.Length == 0)
        {
            Debug.LogWarning("No MeshRenderers found in selection!");
            return;
        }

        int replacementCount = 0;
        
        // Start recording undo
        Undo.RecordObjects(allMeshRenderers, "Replace Materials");

        foreach (var renderer in allMeshRenderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool materialChanged = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].name.Contains(materialNameToReplace))
                {
                    materials[i] = destinationMaterial;
                    materialChanged = true;
                    replacementCount++;
                }
            }

            if (materialChanged)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }

        if (replacementCount > 0)
        {
            Debug.Log($"Replaced {replacementCount} material instances.");
        }
        else
        {
            Debug.Log($"No materials containing '{materialNameToReplace}' found in selection.");
        }
    }
}