static const float PI = 3.1415926535897932384626433832795;

bool hit_sphere(float3 sphere_center, float sphere_radius, float3 ray_dir, float3 ray_origin)
{
	float3 oc = ray_origin - sphere_center;
	float b = 2.0 * dot(oc, ray_dir);
	float c = dot(oc, oc) - sphere_radius * sphere_radius;
	float discriminant = b * b - 4 * c;
	return (discriminant > 0 ? true : false);
}

//taken from https://github.com/hpicgs/cgsee/wiki/Ray-Box-Intersection-on-the-GPU
struct Ray
{
	float3 origin;
	float3 direction;
	float3 inv_direction;
	int3 sign;
};

Ray MakeRay(float3 origin, float3 direction)
{
	float3 inv_direction = float3(1, 1, 1) / direction;
	int3 sign = int3
	(
		(inv_direction.x < 0) ? 1 : 0,
		(inv_direction.y < 0) ? 1 : 0,
		(inv_direction.z < 0) ? 1 : 0
	);

	Ray output;
	output.origin = origin;
	output.direction = direction;
	output.inv_direction = inv_direction;
	output.sign = sign;

	return output;
}

void ray_box_intersection(in Ray ray, in float3 aabb[2], out float tmin, out float tmax)
{
	float tymin, tymax, tzmin, tzmax;
	tmin = (aabb[ray.sign[0]].x - ray.origin.x) * ray.inv_direction.x;
	tmax = (aabb[1 - ray.sign[0]].x - ray.origin.x) * ray.inv_direction.x;
	tymin = (aabb[ray.sign[1]].y - ray.origin.y) * ray.inv_direction.y;
	tymax = (aabb[1 - ray.sign[1]].y - ray.origin.y) * ray.inv_direction.y;
	tzmin = (aabb[ray.sign[2]].z - ray.origin.z) * ray.inv_direction.z;
	tzmax = (aabb[1 - ray.sign[2]].z - ray.origin.z) * ray.inv_direction.z;
	tmin = max(max(tmin, tymin), tzmin);
	tmax = min(min(tmax, tymax), tzmax);
}

float3 GetNormal(float4x4 tri, float3 b)
{
	bool z1_positive = fmod(tri._m23, 2) == 1;
	bool z2_positive = fmod(tri._m23, 4) >= 2;
	bool z3_positive = fmod(tri._m23, 8) >= 4;
	float3 n0 = float3(tri._m30, tri._m31, sqrt(1.0001 - tri._m30 * tri._m30 - tri._m31 * tri._m31) * (z1_positive ? 1 : -1));
	float3 n1 = float3(tri._m32, tri._m33, sqrt(1.0001 - tri._m32 * tri._m32 - tri._m33 * tri._m33) * (z2_positive ? 1 : -1));
	float3 n2 = float3(tri._m03, tri._m13, sqrt(1.0001 - tri._m03 * tri._m03 - tri._m13 * tri._m13) * (z3_positive ? 1 : -1));
	return (n0 * b.x + n1 * b.y + n2 * b.z);
}

//taken from "gpu-based techniques for global illumination effects"
bool ray_triangle_intersection(in Ray ray, float4x4 tri, out float t, out float3 b)
{
	bool ris = false;
	float3 qPrime = tri[0] + tri[1] + tri[2];
	float3 q = qPrime / dot(qPrime, qPrime);
	t = (dot(q, q) - dot(ray.origin, q)) / dot(ray.direction, q);
	if (t > 0.001)
	{
		float3 p = ray.origin + t * ray.direction;
		b = float3(0, 0, 0);
		b.x = dot(tri[0], p);
		b.y = dot(tri[1], p);
		b.z = dot(tri[2], p);
		if (all(b > 0))
		{
			ris = true;
		}
	}
	
	//backface culling
	if (ris)
	{
		float3 normal = GetNormal(tri, b);
		float p = dot(normal, ray.direction);
		ris = (p < 0) ? true : false;
	}

	return ris;
}

bool point_inside_box(float3 p, float3 box_min, float3 box_max)
{
	return (all(p > box_min) && all(p < box_max));
}

//random  numbers with Hammersley sequence
float radicalInverse_VdC(uint bits)
{
	bits = (bits << 16u) | (bits >> 16u);
	bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
	bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
	bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
	bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);

	return float(bits) * 2.3283064365386963e-10; // / 0x100000000
}

float2 Hammersley(uint i, uint n)
{
	return float2((float)i / (float)n, radicalInverse_VdC(i));
}

//random numbers using xorshift and wang hash
uint rand_xorshift(uint rng_state)
{
	// Xorshift algorithm from George Marsaglia's paper
	rng_state ^= (rng_state << 13);
	rng_state ^= (rng_state >> 17);
	rng_state ^= (rng_state << 5);
	return rng_state;
}

uint wang_hash(uint seed)
{
	seed = (seed ^ 61) ^ (seed >> 16);
	seed *= 9;
	seed = seed ^ (seed >> 4);
	seed *= 0x27d4eb2d;
	seed = seed ^ (seed >> 15);
	return seed;
}

//random samples on the hemisphere
float3 HemisphereSample(float u, float v, float3 N)
{
	float phi = v * 2.0 * PI;
	float cosTheta = 1.0 - u;
	float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
	float3 H = float3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);

	float3 UpVector = abs(N.y) != 1.0 ? float3(0, 1, 0) : float3(1, 0, 0);
	float3 TangentX = normalize(cross(N, UpVector));
	float3 TangentY = cross(N, TangentX);
	
	return TangentX * H.x + TangentY * H.y + N * H.z;
}