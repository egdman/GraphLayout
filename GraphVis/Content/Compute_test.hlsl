#if 0
$ubershader (COMPUTE INJECTION|MOVE|REDUCTION|(SIMULATION EULER|RUNGE_KUTTA +LINKS))|(DRAW POINT|LINE)
#endif


#define BLOCK_SIZE 256
#define WARP_SIZE 16
#define HALF_BLOCK BLOCK_SIZE/2

struct PARAMS {
	float4x4	View;
	float4x4	Projection;
	uint		MaxParticles;
	float		DeltaTime;
	float		LinkSize;
};

cbuffer CB1 : register(b0) { 
	PARAMS Params; 
};


struct PARTICLE3D {
	float3	Position;
	float3	Velocity;
	float3	Force;
	float	Energy;
	float	Mass;
	float	Charge;

	float4	Color0;
	float	Size0;
	int		LinksPtr;
	int		LinksCount;
};


struct LinkId {
	int id;
};

struct Link {
	uint par1;
	uint par2;
	float length;
	float force2;
	float3 orientation;
};


SamplerState						Sampler				: 	register(s0);
Texture2D							Texture 			: 	register(t0);

RWStructuredBuffer<PARTICLE3D>		particleRWBuffer	: 	register(u0);
StructuredBuffer<PARTICLE3D>		particleReadBuffer	:	register(t1);

RWStructuredBuffer<float4>			energyRWBuffer		:	register(u1);

StructuredBuffer<LinkId>			linksPtrBuffer		:	register(t2);
StructuredBuffer<Link>				linksBuffer			:	register(t3);



#ifdef COMPUTE

groupshared float4 shPositions[BLOCK_SIZE];
//groupshared float sh_energy[4*BLOCK_SIZE];
groupshared float4 sh_energy[BLOCK_SIZE];




inline float4 pairBodyForce( float4 thisPos, float4 otherPos ) // 4th component is charge
{
	float3 R			= (otherPos - thisPos).xyz;		
	float Rsquared		= R.x * R.x + R.y * R.y + R.z * R.z + 0.1f;
	float Rsixth		= Rsquared * Rsquared * Rsquared;
	float invRCubed		= - otherPos.w / sqrt( Rsixth );	// we will multiply by constants later
	float energy		=   otherPos.w / sqrt( Rsquared );	// we will multiply by constants later
	return float4( mul( invRCubed, R ), energy ); // we write energy into the 4th component
}



float4 springForce( float4 pos, float4 otherPos ) // 4th component in otherPos is link length
{
	float3 R			= (otherPos - pos).xyz;
	float Rabs			= length( R ) + 0.1f;
	float deltaR		= Rabs - otherPos.w;
	float absForce		= 0.1f * ( deltaR ) / ( Rabs );
	float energy		= 0.05f * deltaR * deltaR;
	return float4( mul( absForce, R ), energy );  // we write energy into the 4th component
}


float4 tileForce( float4 position, uint threadId )
{
	float4 force = float4(0, 0, 0, 0);
	float4 otherPosition = float4(0, 0, 0, 0);
	for ( uint i = 0; i < BLOCK_SIZE; ++i )
	{
		otherPosition = shPositions[i];
		force += pairBodyForce( position, otherPosition );
	}
	return force;
}

#define TEST_SIZE 16

float4 tileForce2( float4 position, uint threadId )
{
	float4 force = float4(0, 0, 0, 0);
	float4 otherPosition = float4(0, 0, 0, 0);

	threadId = threadId % TEST_SIZE;
	for ( uint i = 0; i < BLOCK_SIZE - TEST_SIZE; ++i )
	{
		otherPosition = shPositions[i + threadId];
		force += pairBodyForce( position, otherPosition );
	}
	
	for ( uint j = 0; j < TEST_SIZE; ++j )
	{
		uint index = j + threadId + BLOCK_SIZE - TEST_SIZE;
		index = j >= BLOCK_SIZE ? j - BLOCK_SIZE : j;
		otherPosition = shPositions[index];
		force += pairBodyForce( position, otherPosition );
	}
	
	return force;
}




float4 calcRepulsionForce( float4 position, uint3 groupThreadID )
{
	float4 force = float4(0, 0, 0, 0);
	uint tile = 0;
	for ( uint i = 0; i < Params.MaxParticles; i+= BLOCK_SIZE, tile += 1 )
	{
		uint srcId = tile*BLOCK_SIZE + groupThreadID.x;
		PARTICLE3D p = particleRWBuffer[srcId];
		float4 pos = float4( p.Position, p.Charge );
		shPositions[groupThreadID.x] = pos;
		
		GroupMemoryBarrierWithGroupSync();
		
		force += tileForce( position, groupThreadID.x );
	//	force += tileForce2( position, groupThreadID.x );

		GroupMemoryBarrierWithGroupSync();
	}
	return force;
}


float4 calcLinksForce( float4 pos, uint id, uint linkListStart, uint linkCount )
{
	float4 force = float4( 0, 0, 0, 0 );
	PARTICLE3D otherP;
	[allow_uav_condition] for ( uint i = 0; i < linkCount; ++i )
	{
		Link link = linksBuffer[linksPtrBuffer[linkListStart + i].id];
		uint otherId = link.par1;
		if ( id == otherId )
		{
			otherId = link.par2;
		}
		otherP = particleRWBuffer[otherId];
		float4 otherPos = float4( otherP.Position, link.length );
		force += springForce( pos, otherPos );
	}
	return force;
}


#ifdef SIMULATION
[numthreads( BLOCK_SIZE, 1, 1 )]
void CSMain( 
	uint3 groupID			: SV_GroupID,
	uint3 groupThreadID 	: SV_GroupThreadID, 
	uint3 dispatchThreadID 	: SV_DispatchThreadID,
	uint  groupIndex 		: SV_GroupIndex
)
{
	uint id = groupID.x*BLOCK_SIZE + groupThreadID.x;
	
	PARTICLE3D p = particleRWBuffer[id];
	float4 pos = float4 ( p.Position, p.Charge );
	float4 force = float4( 0, 0, 0, 0 );

#ifdef EULER
	force = mul( calcRepulsionForce( pos, groupThreadID ), 10000.0f * pos.w ); // we multiply by all the constants here once
#ifdef LINKS
	force += calcLinksForce ( pos, id, p.LinksPtr, p.LinksCount );
#endif // LINKS
#endif // EULER



#ifdef RUNGE_KUTTA
	force = float4( 0, 0, 0, 0 ); // just a placeholder
#endif // RUNGE_KUTTA

	// add potential well:
//	force.xyz += mul( 0.00005f*length(pos.xyz), -pos.xyz );


	float3 accel = force.xyz;


	p.Force		= force.xyz;
	p.Energy	= force.w;

//	float3 accel = mul( force.xyz, 1/p.Mass );
//	add drag force:
//	accel -= mul ( p.Velocity, 0.7f );
//	p.Force		= accel;
//	p.Energy	= force.w;


	particleRWBuffer[id] = p;
}

#endif // SIMULATION


#ifdef MOVE
[numthreads( BLOCK_SIZE, 1, 1 )]
void CSMain( 
	uint3 groupID			: SV_GroupID,
	uint3 groupThreadID 	: SV_GroupThreadID, 
	uint3 dispatchThreadID 	: SV_DispatchThreadID,
	uint  groupIndex 		: SV_GroupIndex
)
{
	uint id = dispatchThreadID.x;

	if (id < Params.MaxParticles) {
		PARTICLE3D p = particleRWBuffer[ id ];
		
//		p.Position.xyz += mul( p.Velocity, Params.DeltaTime );
//		p.Velocity += mul( p.Force, Params.DeltaTime );

		p.Position.xyz += mul( p.Force, Params.DeltaTime );
		particleRWBuffer[ id ] = p;
	}
}
#endif // MOVE



#ifdef REDUCTION


#if 0
[numthreads( BLOCK_SIZE, 1, 1 )]
void CSMain( 
	uint3 groupID			: SV_GroupID,
	uint3 groupThreadID 	: SV_GroupThreadID, 
	uint3 dispatchThreadID 	: SV_DispatchThreadID,
	uint  groupIndex 		: SV_GroupIndex
)
{
	int id = dispatchThreadID.x;
	sh_energy[groupIndex] = 0;

	if (id < Params.MaxParticles) {
		PARTICLE3D p = particleReadBuffer[ id ];
		float scalarAcc = length(p.Force);

		scalarAcc *= scalarAcc;
		sh_energy[groupIndex] = scalarAcc * p.Mass * p.Mass;
	}
	GroupMemoryBarrierWithGroupSync();


	/*
	// with divergence ( working threads are 0, 2, 4 ... when s = 1 )
	for ( unsigned int s = 1; s < BLOCK_SIZE; s *= 2 )
	{
		if ( groupIndex%(2*s) == 0 )
		{
			sh_energy[groupIndex] += sh_energy[groupIndex + s];
		}
		GroupMemoryBarrierWithGroupSync();
	}
	if( groupIndex == 0 )
	{
		energyRWBuffer[groupID.x] = sh_energy[0];
	}
	*/

	/*
	// without divergence
	for ( unsigned int s = 1; s < BLOCK_SIZE; s *= 2 )
	{
		int index = 2 * s * groupIndex;
		if ( index < BLOCK_SIZE )
		{
			sh_energy[index] += sh_energy[index + s];
		}
		GroupMemoryBarrierWithGroupSync();
	}
	if( groupIndex == 0 )
	{
		energyRWBuffer[groupID.x] = sh_energy[0];
	}
	*/

	
	// sequential addressing without bank conflicts and without divergence:
	for ( unsigned int s = BLOCK_SIZE/2; s > 0; s >>= 1 )
	{
		if ( groupIndex < s )
		{
			sh_energy[groupIndex] += sh_energy[groupIndex + s];
		}
		GroupMemoryBarrierWithGroupSync();
	}
	if( groupIndex == 0 )
	{
		energyRWBuffer[groupID.x] = sh_energy[0];
	}
	
}
#endif


#if 0
[numthreads( HALF_BLOCK, 1, 1 )]
void CSMain( 
	uint3 groupID			: SV_GroupID,
	uint3 groupThreadID 	: SV_GroupThreadID, 
	uint3 dispatchThreadID 	: SV_DispatchThreadID,
	uint  groupIndex 		: SV_GroupIndex
)
{
//	int id = dispatchThreadID.x;
	int id = groupID.x*BLOCK_SIZE + groupIndex;

	// load data into shared memory:
	PARTICLE3D p1 = particleReadBuffer[ id ];
	PARTICLE3D p2 = particleReadBuffer[ id + HALF_BLOCK ];
	float energy1 = p1.Energy;
	float energy2 = p2.Energy;
	float forceSq1 = - (p1.Force.x*p1.Force.x + p1.Force.y*p1.Force.y + p1.Force.z*p1.Force.z);
	float forceSq2 = - (p2.Force.x*p2.Force.x + p2.Force.y*p2.Force.y + p2.Force.z*p2.Force.z);

	sh_energy[groupIndex]					= energy1 + energy2;
	sh_energy[groupIndex +   BLOCK_SIZE]	= forceSq1 + forceSq2;
	sh_energy[groupIndex + 2*BLOCK_SIZE]	= 0;
	sh_energy[groupIndex + 3*BLOCK_SIZE]	= 0;

	GroupMemoryBarrierWithGroupSync();

#if 0
	// partial unroll:
	for ( unsigned int s = BLOCK_SIZE/4; s > WARP_SIZE; s >>= 1 )
	{
		if ( groupIndex < s )
		{
			sh_energy[groupIndex] += sh_energy[groupIndex + s];
		}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( groupIndex < WARP_SIZE )
	{
		sh_energy[groupIndex] += sh_energy[groupIndex + 16];
		sh_energy[groupIndex] += sh_energy[groupIndex + 8];
		sh_energy[groupIndex] += sh_energy[groupIndex + 4];
		sh_energy[groupIndex] += sh_energy[groupIndex + 2];
		sh_energy[groupIndex] += sh_energy[groupIndex + 1];
	}
#endif

//#if 0

	// full unroll:
	if ( HALF_BLOCK >= 512 )
	{
		if ( groupIndex < 256 ) {
			sh_energy[groupIndex]				+= sh_energy[groupIndex + 256];
			sh_energy[groupIndex + BLOCK_SIZE]	+= sh_energy[groupIndex + BLOCK_SIZE + 256];
		}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( HALF_BLOCK >= 256 )
	{
		if ( groupIndex < 128 ) {
			sh_energy[groupIndex]				+= sh_energy[groupIndex + 128];
			sh_energy[groupIndex + BLOCK_SIZE]	+= sh_energy[groupIndex + BLOCK_SIZE + 128];
		}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( HALF_BLOCK >= 128 )
	{
		if ( groupIndex < 64 ) {
			sh_energy[groupIndex]				+= sh_energy[groupIndex + 64];
			sh_energy[groupIndex + BLOCK_SIZE]	+= sh_energy[groupIndex + BLOCK_SIZE + 64];
		}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( HALF_BLOCK >= 64 )
	{
		if ( groupIndex < 32 ) {
			sh_energy[groupIndex]				+= sh_energy[groupIndex + 32];
			sh_energy[groupIndex + BLOCK_SIZE]	+= sh_energy[groupIndex + BLOCK_SIZE + 32];
		}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( groupIndex < 16 )
	{
		sh_energy[groupIndex]				+= sh_energy[groupIndex + 16];
		sh_energy[groupIndex + BLOCK_SIZE]	+= sh_energy[groupIndex + BLOCK_SIZE + 16];

		sh_energy[groupIndex]				+= sh_energy[groupIndex + 8];
		sh_energy[groupIndex + BLOCK_SIZE]	+= sh_energy[groupIndex + BLOCK_SIZE + 8];

		sh_energy[groupIndex]				+= sh_energy[groupIndex + 4];
		sh_energy[groupIndex + BLOCK_SIZE]	+= sh_energy[groupIndex + BLOCK_SIZE + 4];

		sh_energy[groupIndex]				+= sh_energy[groupIndex + 2];
		sh_energy[groupIndex + BLOCK_SIZE]	+= sh_energy[groupIndex + BLOCK_SIZE + 2];

		sh_energy[groupIndex]				+= sh_energy[groupIndex + 1];
		sh_energy[groupIndex + BLOCK_SIZE]	+= sh_energy[groupIndex + BLOCK_SIZE + 1];

	}


//#endif

	if( groupIndex == 0 )
	{
		energyRWBuffer[groupID.x] = sh_energy[0];
		energyRWBuffer[groupID.x + BLOCK_SIZE] = sh_energy[BLOCK_SIZE];
	}

}
#endif


[numthreads( HALF_BLOCK, 1, 1 )]
void CSMain( 
	uint3 groupID			: SV_GroupID,
	uint3 groupThreadID 	: SV_GroupThreadID, 
	uint3 dispatchThreadID 	: SV_DispatchThreadID,
	uint  groupIndex 		: SV_GroupIndex
)
{
//	int id = dispatchThreadID.x;
	int id = groupID.x*BLOCK_SIZE + groupIndex;

	// load data into shared memory:
	PARTICLE3D p1 = particleReadBuffer[ id ];
	PARTICLE3D p2 = particleReadBuffer[ id + HALF_BLOCK ];
	float energy1 = p1.Energy;
	float energy2 = p2.Energy;
	float forceSq1 = - (p1.Force.x*p1.Force.x + p1.Force.y*p1.Force.y + p1.Force.z*p1.Force.z);
	float forceSq2 = - (p2.Force.x*p2.Force.x + p2.Force.y*p2.Force.y + p2.Force.z*p2.Force.z);

	sh_energy[groupIndex] = float4( energy1 + energy2, forceSq1 + forceSq2, 0, 0 );
	GroupMemoryBarrierWithGroupSync();

#if 0
	// partial unroll:
	for ( unsigned int s = BLOCK_SIZE/4; s > WARP_SIZE; s >>= 1 )
	{
		if ( groupIndex < s )
		{
			sh_energy[groupIndex] += sh_energy[groupIndex + s];
		}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( groupIndex < WARP_SIZE )
	{
		sh_energy[groupIndex] += sh_energy[groupIndex + 16];
		sh_energy[groupIndex] += sh_energy[groupIndex + 8];
		sh_energy[groupIndex] += sh_energy[groupIndex + 4];
		sh_energy[groupIndex] += sh_energy[groupIndex + 2];
		sh_energy[groupIndex] += sh_energy[groupIndex + 1];
	}
#endif

//#if 0

	// full unroll:
	if ( HALF_BLOCK >= 512 )
	{
		if ( groupIndex < 256 ) {sh_energy[groupIndex] += sh_energy[groupIndex + 256];}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( HALF_BLOCK >= 256 )
	{
		if ( groupIndex < 128 ) {sh_energy[groupIndex] += sh_energy[groupIndex + 128];}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( HALF_BLOCK >= 128 )
	{
		if ( groupIndex < 64 ) {sh_energy[groupIndex] += sh_energy[groupIndex + 64];}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( HALF_BLOCK >= 64 )
	{
		if ( groupIndex < 32 ) {sh_energy[groupIndex] += sh_energy[groupIndex + 32];}
		GroupMemoryBarrierWithGroupSync();
	}

	if ( groupIndex < 16 )
	{
	/*	if ( BLOCK_SIZE >= 32 )*/ { sh_energy[groupIndex] += sh_energy[groupIndex + 16]; }
	/*	if ( BLOCK_SIZE >= 16 )*/ { sh_energy[groupIndex] += sh_energy[groupIndex +  8]; }
	/*	if ( BLOCK_SIZE >=  8 )*/ { sh_energy[groupIndex] += sh_energy[groupIndex +  4]; }
	/*	if ( BLOCK_SIZE >=  4 )*/ { sh_energy[groupIndex] += sh_energy[groupIndex +  2]; }
	/*	if ( BLOCK_SIZE >=  2 )*/ { sh_energy[groupIndex] += sh_energy[groupIndex +  1]; }
	}


//#endif

	if( groupIndex == 0 )
	{
		energyRWBuffer[groupID.x] = sh_energy[0];
	}

}



#endif // REDUCTION





#endif // COMPUTE





/*-----------------------------------------------------------------------------
	Rendering :
-----------------------------------------------------------------------------*/


#ifdef DRAW


struct VSOutput {
int vertexID : TEXCOORD0;
};

struct GSOutput {
	float4	Position : SV_Position;
	float2	TexCoord : TEXCOORD0;
	float4	Color    : COLOR0;
};



VSOutput VSMain( uint vertexID : SV_VertexID )
{
VSOutput output;
output.vertexID = vertexID;
return output;
}


float Ramp(float f_in, float f_out, float t) 
{
	float y = 1;
	t = saturate(t);
	
	float k_in	=	1 / f_in;
	float k_out	=	-1 / (1-f_out);
	float b_out =	-k_out;	
	
	if (t<f_in)  y = t * k_in;
	if (t>f_out) y = t * k_out + b_out;
	
	
	return y;
}


#ifdef POINT
[maxvertexcount(6)]
void GSMain( point VSOutput inputPoint[1], inout TriangleStream<GSOutput> outputStream )
{

	GSOutput p0, p1, p2, p3;
	
//	VSOutput prt = inputPoint[0];

	PARTICLE3D prt = particleReadBuffer[ inputPoint[0].vertexID ];
	

	float sz = prt.Size0;
	float4 color	=	prt.Color0;
	float4 pos		=	float4( prt.Position.xyz, 1 );
	float4 posV		=	mul( pos, Params.View );

//		p0.Position = mul( float4( position + float2( sz, sz), 0, 1 ), Params.Projection );
	p0.Position = mul( posV + float4( sz, sz, 0, 0 ) , Params.Projection );
//		p0.Position = posP + float4( sz, sz, 0, 0 );		
	p0.TexCoord = float2(1,1);
	p0.Color = color;

//		p1.Position = mul( float4( position + float2(-sz, sz), 0, 1 ), Params.Projection );
	p1.Position = mul( posV + float4(-sz, sz, 0, 0 ) , Params.Projection );
//		p1.Position = posP + float4(-sz, sz, 0, 0 );
	p1.TexCoord = float2(0,1);
	p1.Color = color;

//		p2.Position = mul( float4( position + float2(-sz,-sz), 0, 1 ), Params.Projection );
	p2.Position = mul( posV + float4(-sz,-sz, 0, 0 ) , Params.Projection );
//		p2.Position = posP + float4(-sz,-sz, 0, 0 );
	p2.TexCoord = float2(0,0);
	p2.Color = color;

//		p3.Position = mul( float4( position + float2( sz,-sz), 0, 1 ), Params.Projection );
	p3.Position = mul( posV + float4( sz,-sz, 0, 0 ) , Params.Projection );
//		p3.Position = posP + float4( sz,-sz, 0, 0 );
	p3.TexCoord = float2(1,0);
	p3.Color = color;

	outputStream.Append(p0);
	outputStream.Append(p1);
	outputStream.Append(p2);
	outputStream.RestartStrip();
	outputStream.Append(p0);
	outputStream.Append(p2);
	outputStream.Append(p3);
	outputStream.RestartStrip();

}

#endif // POINT


#ifdef LINE
[maxvertexcount(2)]
void GSMain( point VSOutput inputLine[1], inout LineStream<GSOutput> outputStream )
{
	GSOutput p1, p2;

	Link lk = linksBuffer[ inputLine[0].vertexID ];
	PARTICLE3D end1 = particleReadBuffer[ lk.par1 ];
	PARTICLE3D end2 = particleReadBuffer[ lk.par2 ];
	float4 pos1 = float4( end1.Position.xyz, 1 );
	float4 pos2 = float4( end2.Position.xyz, 1 );

	float4 posV1	=	mul( pos1, Params.View );
	float4 posV2	=	mul( pos2, Params.View );
	

//	PARTICLE3D end1 = particleReadBuffer[inputLine[0].vertexID];
//	float4 pos1 = float4( end1.Position.xyz, 1 );
//	float4 pos2 = float4( end1.Position.xyz + float3(5,0,0), 1 );

//	float4 posV1	=	mul( pos1, Params.View );
//	float4 posV2	=	mul( pos2, Params.View );

	p1.Position		=	mul( posV1, Params.Projection );
	p2.Position		=	mul( posV2, Params.Projection );

	p1.TexCoord		=	float2(0, 0);
	p2.TexCoord		=	float2(0, 0);

	p1.Color		=	float4(0.2f,0.2f,0.2f,0);
	p2.Color		=	float4(0.2f,0.2f,0.2f,0);

	outputStream.Append(p1);
	outputStream.Append(p2);
	outputStream.RestartStrip(); 


}

#endif // LINE



#ifdef LINE
float4 PSMain( GSOutput input ) : SV_Target
{
	return float4(input.Color.rgb,1);
}
#endif // LINE



#ifdef POINT
float4 PSMain( GSOutput input ) : SV_Target
{
	return Texture.Sample( Sampler, input.TexCoord ) * float4(input.Color.rgb,1);
}
#endif // POINT


#endif //DRAW