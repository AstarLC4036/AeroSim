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

// comments were written by DeepSeek v4.1-flash
float heightDensityDecay(float height, float referenceHeight)
{
	// The clamp matters for the light march: a sample ray can dip below the ground, and without it the
	// density turns into exp(+huge) -> Inf/NaN and the pixel goes black.
	return exp(-max(0, height) / referenceHeight);
}

// Both phase functions take the cosine between the view ray and the sun, not the angle. The old code
// applied cos() a second time, which squashed the argument into [0.54, 1] and made the phase almost
// constant, so the anisotropy parameter had no visible effect.
float rayleightPhase(float cosTheta)
{
	return 3.0 / (16.0 * PI) * (1.0 + cosTheta * cosTheta);
}

// g: anisotropicParam. Cornette-Shanks, normalised so the integral over the sphere is 1.
float miePhase(float cosTheta, float g)
{
	float g2 = g * g;
	float a = (1.0 - g2) * (1.0 + cosTheta * cosTheta);
	float b = (2.0 + g2) * pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-4), 1.5);
	return (3.0 / (8.0 * PI)) * (a / b);
}

// Ozone: a thin absorbing layer centred at 25 km. It never scatters, so it only ever shows up in the
// optical depth. It eats red, which is what keeps the zenith blue and stretches the twilight out into
// purple instead of collapsing straight into night.
float ozoneDensityDecay(float height, float ozoneHeight, float ozoneWidth)
{
    return max(0.0, 1.0 - abs(height - ozoneHeight) / ozoneWidth);   // 10 km 处为 0，25 km 处为 1，40 km 处为 0
}