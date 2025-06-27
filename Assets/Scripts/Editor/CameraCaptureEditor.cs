using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MultiCamCapture))]
public class MultiCamCaptureEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MultiCamCapture captureScript = (MultiCamCapture)target;

        EditorGUILayout.Space(20);

        // --- SECTION 1: SETUP TOOL ---
        EditorGUILayout.LabelField("1. Camera Setup Tool (Editor Mode)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select a camera in your scene, frame the shot in the Scene View, then click this button to snap the camera's transform.", MessageType.Info);

        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f); // Blue
        if (GUILayout.Button("Snap Selected Camera to Scene View", GUILayout.Height(35)))
        {
            captureScript.SnapSelectedCameraToView();
        }

        if (GUILayout.Button("Snap to Camera", GUILayout.Height(35)))
        {
            captureScript.AlignSceneViewToSelected();
        }
        GUI.backgroundColor = Color.white;
        
        // Button 3: Manual Capture
        GUI.backgroundColor = new Color(0.9f, 0.5f, 1f); // Purple/Magenta
        if (GUILayout.Button("Manually Capture Selected Camera", GUILayout.Height(35)))
        {
            captureScript.ManualCaptureSelected();
        }
        GUI.backgroundColor = Color.white;


        EditorGUILayout.Space(20);

        // --- SECTION 2: AUTOMATION TOOL ---
        EditorGUILayout.LabelField("2. Automation Tool (Play Mode)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox($"This will find all cameras named '{captureScript.cameraBaseName} (0)', '(1)', etc., and render an image from each. You must be in Play Mode.", MessageType.Info);

        GUI.backgroundColor = new Color(0.4f, 1f, 0.6f); // Green

        // Disable the button if the editor is not in Play Mode
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("Start Automated Capture From All Cameras", GUILayout.Height(40)))
        {
            captureScript.StartAutomatedCapture();
        }
        EditorGUI.EndDisabledGroup();

        GUI.backgroundColor = Color.white;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Button is disabled. Please enter Play Mode to start the automation.", MessageType.Warning);
        }
    }
}