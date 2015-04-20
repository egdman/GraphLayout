#if 0
$ubershader 0
#endif

#pragma kernel CSMain

//NUM_THREADS must match the value of p in the NBodySim script
#define NUM_THREADS 64

#define SHARED_MEMORY_SIZE 256

float _SofteningSquared, _DeltaTime, _Damping;
uint _NumBodies;
float4 _GroupDim, _ThreadDim;
StructuredBuffer<float4> _ReadPos, _ReadVel;
RWStructuredBuffer<float4> _WritePos, _WriteVel;

groupshared float4 sharedPos[SHARED_MEMORY_SIZE];

//This code was ported to direct compute from CUDA
//In cuda the terms for groups and threads are a bit different
//This is what I have changed, with the CUDA terms on the left
//Note - the direct compute terms I have used may not be the offical terms
//threadIdx = threadID
//blockIdx = groupID
//blockDim = threadDim
//gridDim = groupDim

// WRAP is used to force each block to start working on a different 
// chunk (and wrap around back to the beginning of the array) so that
// not all multiprocessors try to read the same memory locations at once.
// Mod without divide, works on values from 0 up to 2m
#define WRAP(x,m) (((x)<m)?(x):(x-m))  

// Macros to simplify shared memory addressing
#define SX(i) sharedPos[i+_ThreadDim.x*threadID.y]
// This macro is only used when multithreadBodies is true (below)
#define SX_SUM(i,j) sharedPos[i+_ThreadDim.x*j]

float3 bodyBodyInteraction(float3 ai, float4 bi, float4 bj) 
{
    float3 r;

    // r_ij  [3 FLOPS]
    r.x = bi.x - bj.x;
    r.y = bi.y - bj.y;
    r.z = bi.z - bj.z;

    // distSqr = dot(r_ij, r_ij) + EPS^2  [6 FLOPS]
    float distSqr = r.x * r.x + r.y * r.y + r.z * r.z;
    distSqr += _SofteningSquared;

    // invDistCube =1/distSqr^(3/2)  [4 FLOPS (2 mul, 1 sqrt, 1 inv)]
    float distSixth = distSqr * distSqr * distSqr;
    float invDistCube = 1.0f / sqrt(distSixth);
    
    // s = m_j * invDistCube [1 FLOP]
    float s = bj.w * invDistCube;

    // a_i =  a_i + s * r_ij [6 FLOPS]
    ai.x += r.x * s;
    ai.y += r.y * s;
    ai.z += r.z * s;

    return ai;
}

// This is the "tile_calculation" function from the GPUG3 article.
float3 gravitation(float4 pos, float3 accel, uint3 threadID)
{
    uint i;

    // Here we unroll the loop
    for (i = 0; i < _ThreadDim.x; ) 
    {
        accel = bodyBodyInteraction(accel, SX(i), pos); i += 1;
        accel = bodyBodyInteraction(accel, SX(i), pos); i += 1;
        accel = bodyBodyInteraction(accel, SX(i), pos); i += 1;
        accel = bodyBodyInteraction(accel, SX(i), pos); i += 1;
    }

    return accel;
}

float3 computeBodyForce(float4 pos, uint3 groupID, uint3 threadID)
{
    float3 acc = float3(0.0, 0.0, 0.0);
    
    //In the GPU gems code multibodies are never used but the code is set up to use them.
    //I have also included the code but how exactly they are to be used is unclear so its disabled here
    bool multithreadBodies = false;
    
    uint p = _ThreadDim.x;
    uint q = _ThreadDim.y;
    uint n = _NumBodies;

    uint start = n/q * threadID.y;
    uint tile0 = start/(n/q);
    uint tile = tile0;
    uint finish = start + n/q;
    
    for (uint i = start; i < finish; i += p, tile++) 
    {
        sharedPos[threadID.x+_ThreadDim.x*threadID.y] = (multithreadBodies) ?
        
        _ReadPos[(WRAP(groupID.x+tile, _GroupDim.x) *_ThreadDim.y + threadID.y ) * _ThreadDim.x + threadID.x] :
         
        _ReadPos[WRAP(groupID.x+tile, _GroupDim.x) * _ThreadDim.x + threadID.x];
        
        GroupMemoryBarrierWithGroupSync();
        // This is the "tile_calculation" function from the GPUG3 article.
        acc = gravitation(pos, acc, threadID);
        GroupMemoryBarrierWithGroupSync();
    }
    
    // When the numBodies / thread block size is < # multiprocessors (16 on G80), the GPU is underutilized
    // For example, with a 256 threads per block and 1024 bodies, there will only be 4 thread blocks, so the 
    // GPU will only be 25% utilized.  To improve this, we use multiple threads per body.  We still can use 
    // blocks of 256 threads, but they are arranged in q rows of p threads each.  Each thread processes 1/q
    // of the forces that affect each body, and then 1/q of the threads (those with threadIdx.y==0) add up
    // the partial sums from the other threads for that body.  To enable this, use the "--p=" and "--q=" 
    // command line options to this example.  e.g.:
    // "nbody.exe --n=1024 --p=64 --q=4" will use 4 threads per body and 256 threads per block. There will be
    // n/p = 16 blocks, so a G80 GPU will be 100% utilized.

    // We use a bool template parameter to specify when the number of threads per body is greater than one, 
    // so that when it is not we don't have to execute the more complex code required!
    if(multithreadBodies)
    {
        SX_SUM(threadID.x, threadID.y).x = acc.x;
        SX_SUM(threadID.x, threadID.y).y = acc.y;
        SX_SUM(threadID.x, threadID.y).z = acc.z;

        GroupMemoryBarrierWithGroupSync();

        // Save the result in global memory for the integration step
        if (threadID.y == 0) 
        {
            for (uint i = 1; i < _ThreadDim.y; i++) 
            {
                acc.x += SX_SUM(threadID.x,i).x;
                acc.y += SX_SUM(threadID.x,i).y;
                acc.z += SX_SUM(threadID.x,i).z;
            }

        }
    }


	return acc;

}

[numthreads(NUM_THREADS,1,1)]
void CSMain (uint3 groupID : SV_GroupID, uint3 threadID : SV_GroupThreadID)
{

	uint index = groupID.x * NUM_THREADS + threadID.x;
	
	float4 pos = _ReadPos[index];
	float4 vel = _ReadVel[index];
	
	float3 force = computeBodyForce(pos, groupID, threadID);
	
	vel.xyz += force.xyz * _DeltaTime;
    vel.xyz *= _Damping;
 
    // new position = old position + velocity * deltaTime
    pos.xyz += vel.xyz * _DeltaTime;
	
	_WritePos[index] = pos;
	_WriteVel[index] = vel;
   
}












