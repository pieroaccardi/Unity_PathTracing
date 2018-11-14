using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TrisCellPair
{
    public uint tris_id;
    public uint cell_id;
}

public struct GridDataEntry
{
    public uint start_offset;
    public uint num_tris;

    public GridDataEntry(uint Start_offset, uint Num_tris)
    {
        start_offset = Start_offset;
        num_tris = Num_tris;
    }
}

public struct UniformGridInfo
{
    public Vector3 size;
    public uint nx;
    public uint ny;
    public uint nz;
    public Vector4 grid_size;
    public Vector4 grid_origin;
}

public class AccelerationStructures
{
    public static readonly int  MAX_TRIS = 1048576;

    public static ComputeBuffer     TriangleBuffer;
    public static ComputeBuffer     TrisVertexBuffer;
    public static int               NumTris;

    public static Bounds            SceneBounds;

    //UNIFORM GRID DATA
    public static ComputeBuffer     IndexList;
    public static ComputeBuffer     GridData;
    public static UniformGridInfo   GridInfo;
    
    public static void BuildTriangleList()
    {
        NumTris = 0;

        float max_x, max_y, max_z;
        max_x = max_y = max_z = -float.MaxValue;
        float min_x, min_y, min_z;
        min_x = min_y = min_z = float.MaxValue;


        if (TriangleBuffer != null)
        {
            TriangleBuffer.Release();
        }

        if (TrisVertexBuffer != null)
        {
            TrisVertexBuffer.Release();
        }

        int current_material_index = 0;
        List<Matrix4x4> triangle_list = new List<Matrix4x4>();
        List<Matrix4x4> vertex_list = new List<Matrix4x4>();

        //gather all scene objects (only mesh renderers, no skinned meshes)
        var renderers = GameObject.FindObjectsOfType<MeshRenderer>();
        foreach (MeshRenderer r in renderers)
        {
            Mesh m = r.gameObject.GetComponent<MeshFilter>().sharedMesh;

            int[] tris = m.triangles;
            int num_tris = tris.Length / 3;
            Vector3[] verts = m.vertices;
            int num_vertices = verts.Length;
            Vector3[] norms = m.normals;

            for (int t = 0; t < num_tris; ++t)
            {
                int base_index = t * 3;
                Vector3 v0 = r.transform.TransformPoint(verts[tris[base_index + 0]]); //vertices are in world space
                Vector3 v1 = r.transform.TransformPoint(verts[tris[base_index + 1]]);
                Vector3 v2 = r.transform.TransformPoint(verts[tris[base_index + 2]]);
                Vector3 n0 = r.transform.TransformDirection(norms[tris[base_index + 0]]); //normals are in world space
                Vector3 n1 = r.transform.TransformDirection(norms[tris[base_index + 1]]);
                Vector3 n2 = r.transform.TransformDirection(norms[tris[base_index + 2]]);
                Matrix4x4 vm = new Matrix4x4();
                vm.SetColumn(0, v0);
                vm.SetColumn(1, v1);
                vm.SetColumn(2, v2);
                                
                Matrix4x4 ivm = vm.InverseSubMatrix();

                //the vertex data in the ivm matrix only uses the 3x3 submatrix, in the remaining i pack the vertex normals
                ivm.m30 = n0.x; ivm.m31 = n0.y;  //row 3, col 0 and 1 for x and y of the first normal
                ivm.m32 = n1.x; ivm.m33 = n1.y;  //row 3, col 2 and 3 for x and y of the second normal
                ivm.m03 = n2.x; ivm.m13 = n2.y;  //col 3, row 0 and 1 for x and y of the third normal

                //in the col 3, row 2 i pack the sign of the z component of the normals. I treat the sign as a boolean (is positive) and pack the 3 booleans in a float
                int z_sign = (n0.z >= 0 ? 1 : 0) | (n1.z >= 0 ? 2 : 0) | (n2.z >= 0 ? 4 : 0);
                ivm.m23 = z_sign;

                triangle_list.Add(ivm);

                vm = new Matrix4x4();
                vm.SetRow(0, v0);
                vm.SetRow(1, v1);
                vm.SetRow(2, v2);

                vertex_list.Add(vm);

                //scene bounding calculation
                CheckRange(v0.x, ref min_x, ref max_x);
                CheckRange(v0.y, ref min_y, ref max_y);
                CheckRange(v0.z, ref min_z, ref max_z);

                CheckRange(v1.x, ref min_x, ref max_x);
                CheckRange(v1.y, ref min_y, ref max_y);
                CheckRange(v1.z, ref min_z, ref max_z);

                CheckRange(v2.x, ref min_x, ref max_x);
                CheckRange(v2.y, ref min_y, ref max_y);
                CheckRange(v2.z, ref min_z, ref max_z);
            }

            current_material_index++;
        }

        if (triangle_list.Count > 0)
        {
            TriangleBuffer = new ComputeBuffer(triangle_list.Count, 64);
            TriangleBuffer.SetData<Matrix4x4>(triangle_list);

            TrisVertexBuffer = new ComputeBuffer(vertex_list.Count, 64);
            TrisVertexBuffer.SetData<Matrix4x4>(vertex_list);

            NumTris = triangle_list.Count;

            Vector3 min = new Vector3(min_x, min_y, min_z) - new Vector3(0.1f, 0.1f, 0.1f);
            Vector3 max = new Vector3(max_x, max_y, max_z) + new Vector3(0.1f, 0.1f, 0.1f);
            SceneBounds = new Bounds((min + max) * 0.5f, max - min);
        }
    }

    public static void BuildUniformGridGPU()
    {
        BuildTriangleList();
        
        if (NumTris <= 0 || NumTris > MAX_TRIS)
        {
            Debug.LogError("Number of tris in scene not valid");
        }

        //compute the number of grid cells (Wald et al. 2006)
        float k = 5;
        float V = SceneBounds.size.x * SceneBounds.size.y * SceneBounds.size.z;
        float m = Mathf.Pow(k * NumTris / V, 1f / 3f);
        int nx = Mathf.CeilToInt(SceneBounds.size.x * m);
        int ny = Mathf.CeilToInt(SceneBounds.size.y * m);
        int nz = Mathf.CeilToInt(SceneBounds.size.z * m);

        GridInfo = new UniformGridInfo();
        GridInfo.size = SceneBounds.size;
        GridInfo.nx = (uint)nx;
        GridInfo.ny = (uint)ny;
        GridInfo.nz = (uint)nz;

        //DEBUG LOG
        Debug.Log("Number  of Triangles: " + NumTris);
        Debug.Log("Number of grid cells: " + nx + " " + ny + " " + nz);

        //create a temporary buffer with foreach triangle the number of overlapped cells
        ComputeBuffer cells_overlapped = new ComputeBuffer(NumTris, 4);

        ComputeShader grid_build_cs = Resources.Load<ComputeShader>("UniformGridBuild");
        int cells_counting_kernel = grid_build_cs.FindKernel("cells_counting");
        int num_groups = Mathf.CeilToInt(NumTris / 32.0f);
        
        Vector4 grid_origin = new Vector4(SceneBounds.min.x, SceneBounds.min.y, SceneBounds.min.z, 0);
        Vector4 grid_size = new Vector4(nx / SceneBounds.size.x, ny / SceneBounds.size.y, nz / SceneBounds.size.z, 0);

        GridInfo.grid_origin = grid_origin;
        GridInfo.grid_size = grid_size;

        //bind shader data
        grid_build_cs.SetBuffer(cells_counting_kernel, "vertices_list", TrisVertexBuffer);
        grid_build_cs.SetBuffer(cells_counting_kernel, "cells_overlapped", cells_overlapped);
        grid_build_cs.SetVector("grid_origin", grid_origin);
        grid_build_cs.SetVector("grid_size", grid_size);
        grid_build_cs.SetInt("num_tris", NumTris);

        grid_build_cs.Dispatch(cells_counting_kernel, num_groups, 1, 1);

        //now with the number of overlapped cells by each triangle, with a prefix sum i get the start index in the indices list and the total number of indices
        //TODO: parallel prefix sum on the GPU would be better
        int[] num_overlapped = new int[NumTris];
        cells_overlapped.GetData(num_overlapped);

        int[] summed = new int[NumTris + 1];
        summed[0] = 0;
        
        for (int i = 1; i <= NumTris; ++i)
        {
            summed[i] = summed[i - 1] + num_overlapped[i - 1];
        }

        //create the index buffer
        int num_pairs = summed[NumTris];
        ComputeBuffer cell_tris_pair_buffer = new ComputeBuffer(num_pairs, 8);  //8 byte for a pair of ints

        //upload the prefix sum result to the GPU
        ComputeBuffer prefix_sum_result = new ComputeBuffer(NumTris + 1, 4);
        prefix_sum_result.SetData(summed);

        //dispatch a compute kernel to find the overlapped cells
        int cells_overlapping_kernel = grid_build_cs.FindKernel("cells_overlapping");
        grid_build_cs.SetBuffer(cells_overlapping_kernel, "vertices_list", TrisVertexBuffer);
        grid_build_cs.SetBuffer(cells_overlapping_kernel, "prefix_sum_result", prefix_sum_result);
        grid_build_cs.SetBuffer(cells_overlapping_kernel, "cell_tris_pair_buffer", cell_tris_pair_buffer);
        grid_build_cs.SetInt("num_cells_x", nx);
        grid_build_cs.SetInt("num_cells_y", ny);

        grid_build_cs.Dispatch(cells_overlapping_kernel, num_groups, 1, 1);

        //next sort the pairs by their cell ID
        //TODO: parallel radix sort on the gpu would be better
        TrisCellPair[] pairs = new TrisCellPair[num_pairs];
        cell_tris_pair_buffer.GetData(pairs);

        System.Array.Sort<TrisCellPair>(pairs, (x, y) => x.cell_id.CompareTo(y.cell_id));

        //create the index list. This is the sorted list of pairs <tris_id, cell_id> after removing the cell_id, so is a list with only tris_id
        uint[] index_list = new uint[num_pairs];
        for (int i = 0; i < index_list.Length; ++i)
            index_list[i] = pairs[i].tris_id;

        if (IndexList != null)
        {
            IndexList.Dispose();
        }
        IndexList = new ComputeBuffer(num_pairs, 4);
        IndexList.SetData(index_list);

        //create the grid data
        //TODO: again, a GPU implementation would be better
        if (GridData != null)
        {
            GridData.Dispose();
        }
        GridData = new ComputeBuffer(nx * ny * nz, 8);

        GridDataEntry[] grid_data = new GridDataEntry[nx * ny * nz];

        uint current_cell = pairs[0].cell_id;
        uint tris_count = 0;
        uint offset = 0;
        for (uint i = 0; i < num_pairs; ++i)
        {
            if (pairs[i].cell_id == current_cell)
            {
                tris_count++;
            }
            else
            {
                grid_data[current_cell] = new GridDataEntry(offset, tris_count);
                offset += tris_count;
                tris_count = 1;
                current_cell = pairs[i].cell_id;
            }
        }

        grid_data[current_cell] = new GridDataEntry(offset, tris_count);

        GridData.SetData(grid_data);

        cell_tris_pair_buffer.Dispose();
        prefix_sum_result.Dispose();
        cells_overlapped.Dispose();

        Debug.Log("UNIFORM GRID BUILD COMPLETE");
    }

    //PRIVATE METHODS
    private static void CheckRange(float x, ref float min, ref float max)
    {
        if (x < min)
            min = x;

        if (x > max)
            max = x;
    }
}
