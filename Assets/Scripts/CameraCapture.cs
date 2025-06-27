using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages a multi-camera capture process. Attach this to a single object.
/// Provides tools to set up cameras and an automated process to render from them.
/// </summary>
public class MultiCamCapture : MonoBehaviour
{
    [Header("Capture Settings")]
    public int imageWidth = 1920;
    public int imageHeight = 1080;

    [Header("File Output")]
    [Tooltip("The folder where the screenshots will be saved.")]
    public string folderPath = "/Volumes/FZUZA_SSDUSB_APPLE/_PROJECTS/MakiGriever/SCREEN/";

    [Header("Automation Settings")]
    [Tooltip("The base name of the cameras to find. e.g., 'Camera' for 'Camera (0)'")]
    public string cameraBaseName = "Camera";
    [Tooltip("The base name of the objects to show/hide. e.g., 'a0stg' for 'a0stg042'")]
    public string objectBaseName = "a0stg";

    private bool isRunning = false;

    #if UNITY_EDITOR
    /// <summary>
    /// [EDITOR ONLY] Snaps the currently selected GameObject to the Scene View's perspective.
    /// </summary>
    public void SnapSelectedCameraToView()
    {
        GameObject selectedObj = Selection.activeGameObject;
        if (selectedObj == null || selectedObj.GetComponent<Camera>() == null)
        {
            Debug.LogError("No camera selected. Please select a camera in the Hierarchy.");
            return;
        }

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogError("Could not find an active Scene View. Please focus the Scene View window.");
            return;
        }

        Undo.RecordObject(selectedObj.transform, "Snap Camera to View");
        selectedObj.transform.position = sceneView.camera.transform.position;
        selectedObj.transform.rotation = sceneView.camera.transform.rotation;
        Debug.Log($"<color=cyan>Snapped '{selectedObj.name}' to the current Scene View.</color>");
    }

    /// <summary>
    /// [EDITOR ONLY] Aligns the Scene View camera to the perspective of the selected GameObject.
    /// </summary>
    public void AlignSceneViewToSelected()
    {
        GameObject selectedObj = Selection.activeGameObject;
        if (selectedObj == null || selectedObj.GetComponent<Camera>() == null)
        {
            Debug.LogError("No camera selected. Please select a camera in the Hierarchy.");
            return;
        }

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogError("Could not find an active Scene View. Please focus the Scene View window.");
            return;
        }
        
        sceneView.AlignViewToObject(selectedObj.transform);
        Debug.Log($"<color=cyan>Scene View aligned to '{selectedObj.name}'.</color>");
    }

    /// <summary>
    /// [EDITOR ONLY] Captures a single image from the currently selected camera.
    /// </summary>
    public void ManualCaptureSelected()
    {
        GameObject selectedObj = Selection.activeGameObject;
        if (selectedObj == null || selectedObj.GetComponent<Camera>() == null)
        {
            Debug.LogError("No camera selected. Please select a camera in the Hierarchy to capture from.");
            return;
        }

        Camera camToCapture = selectedObj.GetComponent<Camera>();
        int cameraIndex = GetIndexFromCameraName(camToCapture.name);

        if (cameraIndex < 0)
        {
            Debug.LogError($"Could not parse a valid index from the camera's name: '{camToCapture.name}'. Name must be like '{cameraBaseName} (5)'.");
            return;
        }

        Debug.Log($"Manually capturing from '{camToCapture.name}'...");
        CaptureFrame(camToCapture, cameraIndex);
        Debug.Log($"<color=green>Manual capture successful for {camToCapture.name}!</color>");
    }
#endif

    /// <summary>
    /// [PLAY MODE ONLY] Starts the automated capture process.
    /// </summary>
    public void StartAutomatedCapture()
    {
        if (isRunning)
        {
            Debug.LogWarning("Automation is already in progress.");
            return;
        }
        if (!Application.isPlaying)
        {
            Debug.LogError("Automated capture must be started in Play Mode.");
            return;
        }
        StartCoroutine(AutomateCaptureRoutine());
    }

    private IEnumerator AutomateCaptureRoutine()
    {
        isRunning = true;

        // 1. Find and prepare all cameras and stage objects
        List<Camera> camerasToCapture = FindAndSortCameras();
        Dictionary<int, GameObject> stageObjects = FindAndCacheStageObjects();

        if (camerasToCapture.Count == 0)
        {
            Debug.LogError($"No cameras found with base name '{cameraBaseName}'.");
            isRunning = false;
            yield break;
        }

        Debug.Log($"Found {camerasToCapture.Count} cameras and {stageObjects.Count} stage objects. Starting automation...");

        // Initially disable all found stage objects
        foreach (var obj in stageObjects.Values) obj.SetActive(false);

        // 2. Iterate, set visibility, and capture from each camera
        foreach (Camera cam in camerasToCapture)
        {
            int cameraIndex = GetIndexFromCameraName(cam.name);
            if (cameraIndex < 0) continue;

            // --- NEW: Object Visibility Logic ---
            // Show the matching stage object, if it exists
            if (stageObjects.TryGetValue(cameraIndex, out GameObject currentStageObject))
            {
                currentStageObject.SetActive(true);
            }
            // ------------------------------------

            Debug.Log($"Capturing from camera: {cam.name}, showing object: {objectBaseName}{cameraIndex:D3}");
            CaptureFrame(cam, cameraIndex);

            yield return new WaitForEndOfFrame();

            // Hide the stage object again to prepare for the next loop
            if (currentStageObject != null)
            {
                currentStageObject.SetActive(false);
            }
        }

        // 3. Cleanup: Restore visibility of all stage objects
        Debug.Log("Automation complete. Restoring object visibility...");
        foreach (var obj in stageObjects.Values) obj.SetActive(true);

        Debug.Log($"<color=green>Process Finished! {camerasToCapture.Count} images saved to {folderPath}</color>");
        isRunning = false;
    }

    private List<Camera> FindAndSortCameras()
    {
        List<Camera> sortedCameras = FindObjectsOfType<Camera>(true) // Find inactive cameras too
            .Where(c => c.name.StartsWith(cameraBaseName) && GetIndexFromCameraName(c.name) >= 0)
            .OrderBy(c => GetIndexFromCameraName(c.name))
            .ToList();

        // Ensure all cameras are enabled for the capture process
        foreach (Camera camera in sortedCameras)
        {
            camera.enabled = true;
        }
        return sortedCameras;
    }

    private Dictionary<int, GameObject> FindAndCacheStageObjects()
    {
        var objectDict = new Dictionary<int, GameObject>();
        var allGameObjects = FindObjectsOfType<GameObject>(true);

        foreach (var go in allGameObjects)
        {
            if (go.name.StartsWith(objectBaseName))
            {
                int index = GetIndexFromObjectName(go.name);
                if (index >= 0 && !objectDict.ContainsKey(index))
                {
                    objectDict.Add(index, go);
                }
            }
        }
        return objectDict;
    }

    private int GetIndexFromCameraName(string name)
    {
        Match match = Regex.Match(name, @"\((\d+)\)");
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }

    private int GetIndexFromObjectName(string name)
    {
        // Extracts the number part from a name like "a0stg042"
        Match match = Regex.Match(name, @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }

    private void CaptureFrame(Camera cam, int fileIndex)
    {
        string fileName = $"{fileIndex:D3}.png";
        string fullPath = Path.Combine(folderPath, fileName);

        RenderTexture renderTexture = new RenderTexture(imageWidth, imageHeight, 24);
        cam.targetTexture = renderTexture;
        Texture2D screenshot = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

        cam.Render();

        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        screenshot.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;

        byte[] bytes = screenshot.EncodeToPNG();

        // Use the correct destroy method depending on the context
        if (Application.isPlaying)
        {
            Destroy(renderTexture);
            Destroy(screenshot);
        }
        else
        {
            DestroyImmediate(renderTexture);
            DestroyImmediate(screenshot);
        }

        try
        {
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            File.WriteAllBytes(fullPath, bytes);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save screenshot {fileName}: {e.Message}");
        }
    }
}
