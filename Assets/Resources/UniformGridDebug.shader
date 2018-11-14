Shader "PathTracing/UniformGridDebug"
{
	Properties
	{
		_Slice("Slice", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
		LOD 100

		Pass
		{
			ZWrite Off
			ZTest Always
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma target 5.0

			#include "UnityCG.cginc"

			StructuredBuffer<uint2> grid_data;
			float4 grid_origin;
			float4 grid_size;
			uint num_cells_x;
			uint num_cells_y;
			uint num_cells_z;

			float _Slice;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 grid_local_position : TEXCOORD0;
				float3 world_position : TEXCOORD1;
				float3 view_direction : TEXCOORD2;
				float3 local_cam : TEXCOORD3;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.world_position = mul(unity_ObjectToWorld, v.vertex);
				o.grid_local_position = (o.world_position - grid_origin) * grid_size;

				o.local_cam = (_WorldSpaceCameraPos - grid_origin) * grid_size;

				o.view_direction = o.grid_local_position - o.local_cam;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				float3 dir = normalize(i.view_direction);

				float z = -i.local_cam.z + _Slice;
				float t = z / dir.z;
				float3 p = t * dir + i.local_cam;
				float mask = ((p.x > num_cells_x || p.x < 0.0) ? 0 : 1) * ((p.y > num_cells_y || p.y < 0.0) ? 0 : 1);

				int3 cell_indices = (int3)(floor(p) + 0.001);

				float2 f = frac(p) - 0.5;
				float ff =  dot(f, f);

				int flat_index = cell_indices.x + cell_indices.y * num_cells_x + cell_indices.z * num_cells_x *  num_cells_y;
				int num_tris = grid_data[flat_index].y;

				float ris = saturate(0.1f + num_tris * 0.1) * mask * (1 - ff);
				
				//cell_indices = (int3)(floor(min(i.grid_local_position, float3(num_cells_x-1, num_cells_y-1, num_cells_z-1))) + 0.001);
				//flat_index = cell_indices.x + cell_indices.y * num_cells_x + cell_indices.z * num_cells_x *  num_cells_y;
				//ris = grid_data[flat_index].y * 0.25;

				return float4(ris, ris, ris, 0.7);
			}
			ENDCG
		}
	}
}
