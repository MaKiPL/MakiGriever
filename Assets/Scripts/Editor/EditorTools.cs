using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.Formats.Fbx.Exporter;
using Directory = UnityEngine.Windows.Directory;
using File = UnityEngine.Windows.File;

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
        [MenuItem("MakiGriever/Export Selected to FBX with Textures")]
    private static void ExportSelectedToFbx()
    {
        // 1. Get the selected GameObjects
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "Please select one or more GameObjects to export.", "OK");
            return;
        }

        // 2. Get the path to save the FBX file
        string defaultName = selectedObjects.Length > 1 ? "CombinedExport" : selectedObjects[0].name;
        string path = EditorUtility.SaveFilePanel("Save FBX", "", defaultName + ".fbx", "fbx");
        if (string.IsNullOrEmpty(path))
        {
            return; // User cancelled the save dialog
        }

        // 3. Create temporary folders for the assets
        string tempExportFolder = "Assets/TempFbxExport";
        if (Directory.Exists(tempExportFolder))
        {
            AssetDatabase.DeleteAsset(tempExportFolder);
        }
        Directory.CreateDirectory(tempExportFolder);
        string tempMaterialsFolder = Path.Combine(tempExportFolder, "Materials");
        Directory.CreateDirectory(tempMaterialsFolder);
        string tempTexturesFolder = Path.Combine(tempExportFolder, "Textures");
        Directory.CreateDirectory(tempTexturesFolder);

        // 4. Create a temporary parent object to hold all the clones
        GameObject exportRootObject = new GameObject("FBXExportRoot");

        try
        {
            // Clone each selected object and parent it to our temporary root
            foreach (var selectedObject in selectedObjects)
            {
                GameObject clone = Instantiate(selectedObject, exportRootObject.transform);
                clone.name = selectedObject.name; // Preserve original name
            }

            // 5. Process all renderers in the temporary hierarchy to create concrete assets
            var renderers = exportRootObject.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                var sharedMaterials = renderer.sharedMaterials;
                var newMaterials = new Material[sharedMaterials.Length];

                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material originalMat = sharedMaterials[i];
                    if (originalMat == null) continue;

                    Material newMat = new Material(originalMat);

                    if (newMat.mainTexture != null && newMat.mainTexture is Texture2D originalTex)
                    {
                        string texturePath = Path.Combine(tempTexturesFolder, originalTex.name + ".png");
                        
                        if (!File.Exists(Path.GetFullPath(texturePath)))
                        {
                            Texture2D readableTex = MakeTextureReadable(originalTex);
                            byte[] pngData = readableTex.EncodeToPNG();
                            File.WriteAllBytes(texturePath, pngData);
                            if (readableTex != originalTex) DestroyImmediate(readableTex);
                            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                        }

                        Texture2D newTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        newMat.mainTexture = newTex;
                    }

                    string materialPath = Path.Combine(tempMaterialsFolder, originalMat.name + ".mat");
                    materialPath = AssetDatabase.GenerateUniqueAssetPath(materialPath);
                    AssetDatabase.CreateAsset(newMat, materialPath);
                    newMaterials[i] = newMat;
                }
                renderer.sharedMaterials = newMaterials;
            }

            // 6. Export the temporary root object, which now contains all selected objects
            ExportModelOptions exportOptions = new ExportModelOptions
            {
                EmbedTextures = true,
                ExportFormat = ExportFormat.Binary
            };
            ModelExporter.ExportObject(path, exportRootObject, exportOptions);
            EditorUtility.DisplayDialog("Export Successful", $"Exported {selectedObjects.Length} object(s) to:\n{path}", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"FBX Export failed: {e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("Export Failed", "An error occurred during export. Check the console for details.", "OK");
        }
        finally
        {
            // 7. Clean up by destroying the temporary root object and the asset folder
            DestroyImmediate(exportRootObject);
            AssetDatabase.DeleteAsset(tempExportFolder);
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// Creates a readable copy of a texture if the source is not readable.
    /// </summary>
    private static Texture2D MakeTextureReadable(Texture2D source)
    {
        if (source.isReadable)
        {
            return source;
        }

        RenderTexture renderTex = RenderTexture.GetTemporary(
                    source.width, source.height, 0,
                    RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

        Graphics.Blit(source, renderTex);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;
        Texture2D readableText = new Texture2D(source.width, source.height);
        readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableText.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);
        return readableText;
    }
}