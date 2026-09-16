#ifndef TSUKUYOMI_OPENPBR_TYPES_INCLUDED
#define TSUKUYOMI_OPENPBR_TYPES_INCLUDED

// Opaque OpenPBR 1.1.1 subset. Linear rendering-space RGB; directions point
// away from the surface. No URP types or UnityPerMaterial declarations here.
static const float TSU_OPBR_PI = 3.14159265358979323846;
static const float TSU_OPBR_INV_PI = 0.31830988618379067154;
static const float TSU_OPBR_MIN_ALPHA = 0.0016; // finite raster footprint

struct TsukuyomiOpenPBRMaterial
{
    float3 baseColor;
    float metalness;
    float diffuseRoughness;
    float specularRoughness;
    float specularIOR;              // exterior air, supported range [1, 3]
    float anisotropy;               // [0, 1], tangent is the major axis
    float coatWeight;
    float3 coatColor;               // round-trip normal-incidence transmittance
    float coatRoughness;
    float coatIOR;
    float coatDarkening;
    float fuzzWeight;
    float3 fuzzColor;
    float fuzzRoughness;
};

struct TsukuyomiOpenPBRGeometry
{
    float3 geometricNormalWS;
    float3 normalWS;
    float3 tangentWS;
    float tangentSign;
    float3 coatNormalWS;
    float3 viewDirectionWS;
};

struct TsukuyomiOpenPBRResponse
{
    float3 diffuse;
    float3 specular;
};

struct TsukuyomiOpenPBRSpecularState
{
    float3 N, T, B, V;
    float2 alpha;
    float roughness, NoV, ior, metalness;
    float3 f0;
    float3 msColor;
    float4 dfg;                     // A, B, average ideal energy, directional ideal energy
    float3 energyBalanced, energyHigh;
};

struct TsukuyomiOpenPBRPrepared
{
    TsukuyomiOpenPBRMaterial material;
    TsukuyomiOpenPBRGeometry geometry;
    TsukuyomiOpenPBRSpecularState baseSpecular, coatSpecular;
    float3 fuzzN, fuzzT, fuzzB;
    float3 fuzzLtc;
    float fuzzEnergy;
    float3 coatDarkening;
    float diffuseViewAlbedoBalanced, diffuseViewAlbedoHigh;
};

// Radiance inputs are normalized convolutions: a constant white environment
// supplies 1 in every field. Diffuse is irradiance / PI (URP bakedGI units).
struct TsukuyomiOpenPBREnvironment
{
    float3 diffuse;
    float3 baseSpecular;
    float3 coatSpecular;
    float3 fuzz;
};

TsukuyomiOpenPBRMaterial TsukuyomiOpenPBRDefaultMaterial()
{
    TsukuyomiOpenPBRMaterial m = (TsukuyomiOpenPBRMaterial)0;
    m.baseColor = 0.8;
    m.specularRoughness = 0.3;
    m.specularIOR = 1.5;
    m.coatColor = 1.0;
    m.coatIOR = 1.6;
    m.coatDarkening = 1.0;
    m.fuzzColor = 1.0;
    m.fuzzRoughness = 0.5;
    return m;
}

float3 TsuOPBRNormalize(float3 v, float3 fallback)
{
    float l2 = dot(v, v);
    return l2 > 1e-12 ? v * rsqrt(max(l2, 1e-12)) : fallback;
}

void TsuOPBRBasis(float3 N, float3 tangent, float sign, out float3 T, out float3 B)
{
    float3 axis = abs(N.z) < 0.999 ? float3(0, 0, 1) : float3(0, 1, 0);
    T = TsuOPBRNormalize(tangent - N * dot(tangent, N), normalize(cross(axis, N)));
    B = cross(N, T) * (sign < 0.0 ? -1.0 : 1.0);
}

float TsuOPBRPow5(float x) { float x2 = x * x; return x2 * x2 * x; }
float TsuOPBRF0(float eta) { float x = (eta - 1.0) / (eta + 1.0); return x * x; }

float TsuOPBRFresnel(float mu, float eta)
{
    if (abs(eta - 1.0) < 1e-5) return 0.0;
    mu = saturate(mu);
    float ct = sqrt(max(0.0, 1.0 - (1.0 - mu * mu) / (eta * eta)));
    float rs = (mu - eta * ct) / max(mu + eta * ct, 1e-7);
    float rp = (eta * mu - ct) / max(eta * mu + ct, 1e-7);
    return 0.5 * (rs * rs + rp * rp);
}

#endif
