using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PathTracer : MonoBehaviour
{
    //PRIVATE FIELDS
    private Camera          scene_view_camera;
    private Vector3         last_camera_position;
    private Quaternion      last_camera_rotation;
    private Matrix4x4       worldspace_frustum_corners;

    private ComputeShader   path_tracing_CS;
    private int             groups_x;
    private int             groups_y;
    private int             path_tracing_kernel;

    private Material        tonemap_blit;

    private RenderTexture   hdr_rt;

    //PUBLIC METHODS
    public void Setup(Camera cam)
    {
        //I must call this function every time the viewport is resized, VERY IMPORTANT

        scene_view_camera = cam;

        path_tracing_CS = Resources.Load<ComputeShader>("PathTracingCS");
        path_tracing_kernel = path_tracing_CS.FindKernel("PathTrace_uniform_grid");
        path_tracing_CS.SetVector("screen_size", new Vector4(cam.pixelRect.width, cam.pixelRect.height, 0, 0));
        
        groups_x = Mathf.CeilToInt(cam.pixelRect.width / 8.0f);
        groups_y = Mathf.CeilToInt(cam.pixelRect.height / 8.0f);

        tonemap_blit = new Material(Shader.Find("PathTracing/Tonemap"));

        hdr_rt = new RenderTexture((int)cam.pixelRect.width, (int)cam.pixelRect.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        hdr_rt.enableRandomWrite = true;
        hdr_rt.Create();

        AccelerationStructures.BuildUniformGridGPU();
        SetUniformGrid();
    }

    public void Dispose()
    {
        hdr_rt.Release();
    }

    public void SetUniformGrid()
    {
        path_tracing_CS.SetBuffer(path_tracing_kernel, "triangle_list", AccelerationStructures.TriangleBuffer);
        path_tracing_CS.SetBuffer(path_tracing_kernel, "grid_data", AccelerationStructures.GridData);
        path_tracing_CS.SetBuffer(path_tracing_kernel, "index_list", AccelerationStructures.IndexList);
        path_tracing_CS.SetInt("num_tris", AccelerationStructures.NumTris);
        path_tracing_CS.SetVector("grid_min", AccelerationStructures.SceneBounds.min);
        path_tracing_CS.SetVector("grid_max", AccelerationStructures.SceneBounds.max);
        path_tracing_CS.SetVector("grid_origin", AccelerationStructures.GridInfo.grid_origin);
        path_tracing_CS.SetVector("grid_size", AccelerationStructures.GridInfo.grid_size);
        path_tracing_CS.SetInt("num_cells_x", (int)AccelerationStructures.GridInfo.nx);
        path_tracing_CS.SetInt("num_cells_y", (int)AccelerationStructures.GridInfo.ny);
        path_tracing_CS.SetInt("num_cells_z", (int)AccelerationStructures.GridInfo.nz);
    }

    //PRIVATE METHODS
    private void Update()
    {
        Debug.Log("UPDATE");
    }
    

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (transform.position != last_camera_position || transform.rotation != last_camera_rotation)
        {
            ResetBuffer();
        }

        last_camera_position = transform.position;
        last_camera_rotation = transform.rotation;

        Vector3[] frustumCorners = new Vector3[4];
        scene_view_camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), scene_view_camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
        worldspace_frustum_corners.SetRow(0, scene_view_camera.transform.TransformVector(frustumCorners[0]));
        worldspace_frustum_corners.SetRow(1, scene_view_camera.transform.TransformVector(frustumCorners[1]));
        worldspace_frustum_corners.SetRow(2, scene_view_camera.transform.TransformVector(frustumCorners[3]));
        worldspace_frustum_corners.SetRow(3, scene_view_camera.transform.TransformVector(frustumCorners[2]));
        path_tracing_CS.SetMatrix("worldspace_frustum_corners", worldspace_frustum_corners);
        path_tracing_CS.SetVector("camera_position", scene_view_camera.transform.position);
        
        path_tracing_CS.SetTexture(path_tracing_kernel, "output", hdr_rt);

        int random_seed = Random.Range(0, int.MaxValue / 100);
        path_tracing_CS.SetInt("start_seed", random_seed);

        path_tracing_CS.Dispatch(path_tracing_kernel, groups_x, groups_y, 1);
        
        Graphics.Blit(hdr_rt, destination, tonemap_blit, 0);
    }

    private void ResetBuffer()
    {
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = hdr_rt;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = old;
    }
}