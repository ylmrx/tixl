RWStructuredBuffer<uint> BucketCounter :register(u2);      

cbuffer Params : register(b0)
{
    int BucketCount;
    int ParticleCount;
}


#define THREADS_PER_GROUP 64

 // 1. ClearBucketCounter.compute
// Sets all bucket counts to 0
[numthreads(THREADS_PER_GROUP, 1, 1)]
void ClearBucketCounter(uint3 DTid : SV_DispatchThreadID)
{
    if (DTid.x < BucketCount)
        BucketCounter[DTid.x] = 0;
}
