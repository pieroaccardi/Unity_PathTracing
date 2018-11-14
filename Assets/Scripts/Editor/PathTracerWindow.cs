using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class PathTracerWindow : EditorWindow
{
    [MenuItem("Window/PathTracer")]
    private static void Init()
    {
        // Get existing open window or if none, make a new one:
        PathTracerWindow window = (PathTracerWindow)EditorWindow.GetWindow(typeof(PathTracerWindow));
        window.Show();
    }

    private void Setup()
    {

    }

    private void OnGUI()
    {
        if (GUILayout.Button("Start Render"))
        {
            PathTracer p = SceneView.lastActiveSceneView.camera.gameObject.GetComponent<PathTracer>();
            if (p == null)
            {
                p = SceneView.lastActiveSceneView.camera.gameObject.AddComponent<PathTracer>();
                p.Setup(SceneView.lastActiveSceneView.camera);
            }
        }

        if (GUILayout.Button("Stop Render"))
        {
            PathTracer p = SceneView.lastActiveSceneView.camera.gameObject.GetComponent<PathTracer>();
            if (p != null)
            {
                p.Dispose();
                Object.DestroyImmediate(p);
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Debug Uniform Grid"))
        {
            GameObject existing_grid = GameObject.Find("debug_grid");
            if (existing_grid != null)
                Object.DestroyImmediate(existing_grid.gameObject);

            AccelerationStructures.BuildUniformGridGPU();

            GameObject grid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grid.name = "debug_grid";
            grid.transform.position = AccelerationStructures.SceneBounds.center;
            grid.transform.localScale = AccelerationStructures.SceneBounds.size;

            Material grid_material = new Material(Shader.Find("PathTracing/UniformGridDebug"));

            grid_material.SetBuffer("grid_data", AccelerationStructures.GridData);

            grid_material.SetVector("grid_origin", AccelerationStructures.GridInfo.grid_origin);
            grid_material.SetVector("grid_size", AccelerationStructures.GridInfo.grid_size);
            grid_material.SetInt("num_cells_x", (int)AccelerationStructures.GridInfo.nx);
            grid_material.SetInt("num_cells_y", (int)AccelerationStructures.GridInfo.ny);
            grid_material.SetInt("num_cells_z", (int)AccelerationStructures.GridInfo.nz);
            grid.GetComponent<MeshRenderer>().material = grid_material;
        }
    }
}
