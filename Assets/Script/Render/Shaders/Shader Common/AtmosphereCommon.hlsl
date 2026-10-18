#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Sphere raycast method form SebLague's Shader
// Returns vector (dstToSphere, dstThroughSphere)
// If ray origin is inside sphere, dstToSphere = 0
// If ray misses sphere, dstToSphere = infinity; dstThroughSphere = 0
float2 raySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir) {
	float3 offset = rayOrigin - sphereCentre;
	float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
	float b = 2 * dot(offset, rayDir);
	float c = dot(offset, offset) - sphereRadius * sphereRadius;
	float d = b * b - 4 * a * c; // Discriminant from quadratic formula

	// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
	if (d > 0) {
		float s = sqrt(d);
		float dstToSphereNear = max(0, (-b - s) / (2 * a));
		float dstToSphereFar = (-b + s) / (2 * a);

		// Ignore intersections that occur behind the ray
		if (dstToSphereFar >= 0) {
			return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
		}
	}
	// Ray did not intersect sphere
	return float2(1.#INF, 0);
}

// Compress atmosphere lum
float3 compressScreenLuminance(float3 hdrColor, float knee = 1.0, float maxLum = 1.5)
{
    // Rec.709 lum
	const float3 LUMA = float3(0.2126, 0.7152, 0.0722);
	float lum = dot(hdrColor, LUMA);

    // soft compress: Reinhard curve compress
	float lumC = lum;
	if (lum > knee)
	{
		float x = lum - knee;
		float range = max(maxLum - knee, 1e-5);
		lumC = knee + x / (1.0 + x / range);
	}

    // scale rgb
	float scale = lumC / max(lum, 1e-5);

    // avoid NaNs
	scale = (lum < 1e-5) ? 1.0 : scale;

	return hdrColor * scale;
}

float heightDensityDecay(float height, float referenceHeight)
{
	return exp(-height / referenceHeight);
}

float rayleightPhase(float theta)
{
	float cosine = cos(theta);
	return 3 / (16 * PI) * (1 + cosine * cosine);
}

// g: anisotropicParam
float miePhase(float theta, float g)
{
	float pow2g = g * g;
	float cosineTheta = cos(theta);
	float pow2CosTheta = cosineTheta * cosineTheta;
	float a = (1 - pow2g) * (1 + pow2CosTheta);
	float b = (2 + pow2g) * pow(1 + pow2g - 2 * g * cosineTheta, 1.5);
	return (3 / 8 * PI) * (a / b);
}