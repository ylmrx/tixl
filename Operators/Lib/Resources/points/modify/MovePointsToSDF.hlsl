#include "shared/point.hlsl"
#include "shared/quat-functions.hlsl"

/*{ADDITIONAL_INCLUDES}*/

cbuffer Params : register(b0)
{
    float Amount;
    float ClampDistance;
    float MinDistance;
    float StepDistanceFactor;

    float NormalSamplingDistance;
}

cbuffer Params : register(b1)
{
    /*{FLOAT_PARAMS}*/
}

cbuffer Params : register(b2)
{
    int MaxSteps;
    int WriteDistanceMode;
    int SetOrientation;
}

StructuredBuffer<Point> SourcePoints : t0;
RWStructuredBuffer<Point> ResultPoints : u0;

//=== Global functions ==============================================
/*{GLOBALS}*/

//=== Additional Resources ==========================================
/*{RESOURCES(t1)}*/

//=== Field functions ===============================================
/*{FIELD_FUNCTIONS}*/

//-------------------------------------------------------------------
float4 GetField(float4 p)
{
    float4 f = 1;
    /*{FIELD_CALL}*/
    return f;
}

inline float GetDistance(float3 p3)
{
    return GetField(float4(p3.xyz, 0)).w;
}

float3 GetNormal(float3 p)
{
    return normalize(
        GetDistance(p + float3(NormalSamplingDistance, -NormalSamplingDistance, -NormalSamplingDistance)) * float3(1, -1, -1) +
        GetDistance(p + float3(-NormalSamplingDistance, NormalSamplingDistance, -NormalSamplingDistance)) * float3(-1, 1, -1) +
        GetDistance(p + float3(-NormalSamplingDistance, -NormalSamplingDistance, NormalSamplingDistance)) * float3(-1, -1, 1) +
        GetDistance(p + float3(NormalSamplingDistance, NormalSamplingDistance, NormalSamplingDistance)) * float3(1, 1, 1));
}
//===================================================================

static const float NoisePhase = 0;

#define ModeOverride 0
#define ModeAdd 1
#define ModeSub 2
#define ModeMultiply 3
#define ModeInvert 4

[numthreads(64, 1, 1)] void main(uint3 i : SV_DispatchThreadID)
{
    uint numStructs, stride;
    SourcePoints.GetDimensions(numStructs, stride);
    if (i.x >= numStructs)
        return;

    Point p = SourcePoints[i.x];

    if (isnan(p.Scale.x))
    {
        ResultPoints[i.x] = p;
        return;
    }

    float3 pos = p.Position;

    float3 pp = pos;
    for (int stepIndex = 0; stepIndex < MaxSteps; stepIndex++)
    {
        float d = GetDistance(pp);
        if (abs(d) < MinDistance)
            break;

        float3 n = GetNormal(pp);
        pp -= n * d * StepDistanceFactor;
    }

    float3 total = pos - pp;
    if (WriteDistanceMode > 0)
    {
        float totalDistance = length(total);
        if (WriteDistanceMode == 1)
        {
            p.FX1 = totalDistance;
        }
        else
        {
            p.FX2 = totalDistance;
        }
    }
    p.Position = lerp(p.Position, pp, Amount);

    if (SetOrientation)
    {
        p.Rotation = qSlerp(p.Rotation, normalize(qLookAt(total, float3(0, 1, 0))), Amount);
    }

    ResultPoints[i.x] = p;
}
