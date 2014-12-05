
#if 0
$compute INJECTION|SIMULATION|MOVE EULER|RUNGE_KUTTA
$geometry
$pixel
$vertex
#endif

#define BLOCK_SIZE	512


struct PARTICLE3D {
	float4	Position; // 3 coordinates + mass
	float3	Velocity;
	float4	Color0;
	float	Size0;
	float	TotalLifeTime;
	float	LifeTime;
	int		LinkedTo1;
	int		LinkedTo2;
	float3	Acceleration;
};


struct PARAMS {
	float4x4	View;
	float4x4	Projection;
	int			MaxParticles;
	float		DeltaTime;
	float		Mass;
};

cbuffer CB1 : register(b0) { 
	PARAMS Params; 
};

SamplerState						Sampler				: 	register(s0);
Texture2D							Texture 			: 	register(t0);
RWStructuredBuffer<PARTICLE3D>		particleBufferSrc	: 	register(u0);
StructuredBuffer<PARTICLE3D>		GSResourceBuffer	:	register(u1);
//AppendStructuredBuffer<PARTICLE3D>	particleBufferDst	: 	register(u0);

// group shared array for body coordinates:
groupshared float4 shPositions[BLOCK_SIZE];

/*-----------------------------------------------------------------------------
	Simulation :
-----------------------------------------------------------------------------*/


struct BodyState
{
	float4 Position;
	float3 Velocity;
	float3 Acceleration;
	uint id;
};


struct Derivative
{
	float3 dxdt;
	float3 dvdt;
};



float3 twoBodyAccel( in float4 bodyState, in float4 otherBodyState )
{
	float3 R			= otherBodyState.xyz - bodyState.xyz;			
//	float softenerSq	= 0.1f; 
	float Rsquared		= R.x * R.x + R.y * R.y + R.z * R.z + 0.1f;
	float Rabs			= sqrt( Rsquared );
	float Rsixth		= Rsquared * Rsquared * Rsquared;
	float invRCubed		= /*- otherBodyState.w / sqrt( Rsixth ) + */10.0f * ( Rabs - 150.0f ) / ( bodyState.w * Rabs );
	return mul( invRCubed, R );
}

/*
float3 calculateTile( in float4 bodyState, in float3 accel )
{
	for ( uint i = 0; i < BLOCK_SIZE; ++i ) {

		accel += twoBodyAccel( bodyState, shPositions[i] );	

	}
	return accel;
}*/

/*
float3 Acceleration_SHARED( in BodyState state, in uint threadIndex, in uint numParticles )
{
	// cancel out self-interaction:
	float3 acc = - twoBodyAccel( state.Position, particleBufferSrc[state.id].Position );

	uint tileNum = 0;

	for ( uint i = 0; i < numParticles; i += BLOCK_SIZE ) {
		uint srcIndex = tileNum * BLOCK_SIZE + threadIndex;

		shPositions[threadIndex] = particleBufferSrc[srcIndex].Position;
		
		// barrier sync:
		GroupMemoryBarrier();

		acc = calculateTile( state.Position, acc );
		// barrier sync:
		GroupMemoryBarrier();
		tileNum++;
	}

	return acc;
}*/


float3 Acceleration( in PARTICLE3D prt )
{
	float3 acc = {0,0,0};
	PARTICLE3D other = particleBufferSrc[ prt.LinkedTo1 ];
	acc += twoBodyAccel( prt.Position, other.Position );
	other = particleBufferSrc[ prt.LinkedTo2 ];
	acc += twoBodyAccel( prt.Position, other.Position );
	return acc;
}


/*
Derivative Evaluate_SHARED( BodyState state, float dt, Derivative der, uint threadIndex, uint numParticles )
{
	state.Position.xyz += mul( der.dxdt, dt );
	state.Velocity += mul( der.dvdt, dt );

	Derivative output;
	output.dxdt = state.Velocity;
	output.dvdt = Acceleration_SHARED( state, threadIndex, numParticles );

	return output;
}*/



void IntegrateEUL_SHARED( inout BodyState state, in float dt, in uint threadIndex, in uint numParticles )
{
	
	state.Acceleration	= Acceleration( particleBufferSrc[state.id] );
}



/*
void IntegrateRK4_SHARED( inout BodyState state, in float dt, in uint threadIndex, in uint numParticles )
{
	Derivative init;
	init.dxdt	=	float3( 0, 0, 0 );
	init.dvdt	=	float3( 0, 0, 0 );
	Derivative a;
	Derivative b;
	Derivative c;
	Derivative d;

	a	=	Evaluate_SHARED( state,   0.0f,		init,		threadIndex, numParticles );
	b	=	Evaluate_SHARED( state,   0.5f * dt,	a,		threadIndex, numParticles );
	c	=	Evaluate_SHARED( state,   0.5f * dt,	b,		threadIndex, numParticles );
	d	=	Evaluate_SHARED( state,   dt,			c,		threadIndex, numParticles );

	float3 dxdt	=	1.0f / 6.0f * ( a.dxdt + 2.0f * ( b.dxdt + c.dxdt ) + d.dxdt );
	float3 dvdt	=	1.0f / 6.0f * ( a.dvdt + 2.0f * ( b.dvdt + c.dvdt ) + d.dvdt );

	state.Position.xyz	+=	mul( dxdt, dt );
	state.Velocity	+=	mul( dvdt, dt );
}
*/


[numthreads( BLOCK_SIZE, 1, 1 )]
void CSMain( 
	uint3 groupID			: SV_GroupID,
	uint3 groupThreadID 	: SV_GroupThreadID, 
	uint3 dispatchThreadID 	: SV_DispatchThreadID,
	uint  groupIndex 		: SV_GroupIndex
)
{
	int id = dispatchThreadID.x;

#ifdef INJECTION
	if (id < Params.MaxParticles) {
		PARTICLE3D p = particleBufferSrc[id];
		
		if (p.LifeTime < p.TotalLifeTime) {
			
			particleBufferSrc[id] = p;
		}
	}
#endif

#ifdef SIMULATION
	if (id < Params.MaxParticles) {
		PARTICLE3D p = particleBufferSrc[ id ];
		
		if (p.LifeTime < p.TotalLifeTime) {
			p.LifeTime += Params.DeltaTime;

			uint numParticles	=	0;
			uint stride			=	0;
			particleBufferSrc.GetDimensions( numParticles, stride );


			BodyState state;
			state.Position		=	p.Position;
			state.Velocity		=	p.Velocity;
			state.Acceleration	=	p.Acceleration;
			state.id			=	id;

#ifdef EULER

			IntegrateEUL_SHARED( state, Params.DeltaTime, groupIndex, numParticles );

#endif
#ifdef RUNGE_KUTTA
	
			IntegrateEUL_SHARED( state, Params.DeltaTime, groupIndex, numParticles );

#endif

			float accel	=	state.Acceleration;

			float maxAccel = 7.0f;
			accel = saturate( accel / maxAccel );

			p.Color0	=	float4( accel, - 0.5f * accel +1.0f, - 0.5f * accel +1.0f, 1 );

			p.Acceleration = state.Acceleration;
			particleBufferSrc[id] = p;
		}
	}
#endif
#ifdef MOVE
	if (id < Params.MaxParticles) {
		PARTICLE3D p = particleBufferSrc[ id ];
		
		p.Position.xyz += mul( p.Velocity, Params.DeltaTime );
		p.Velocity += mul( p.Acceleration, Params.DeltaTime );
		particleBufferSrc[ id ] = p;
	}
#endif


}







/*-----------------------------------------------------------------------------
	Rendering :
-----------------------------------------------------------------------------*/
/*

struct VSOutput {
	float4	Position		:	POSITION;
	float4	Color0			:	COLOR0;

	float	Size0			:	PSIZE;

	float	TotalLifeTime	:	TEXCOORD0;
	float	LifeTime		:	TEXCOORD1;
};*/


struct VSOutput {
int vertexID : TEXCOORD0;
};

struct GSOutput {
	float4	Position : SV_Position;
	float2	TexCoord : TEXCOORD0;
	float4	Color    : COLOR0;
};

/*
VSOutput VSMain( uint vertexID : SV_VertexID )
{
	PARTICLE prt = particleBufferSrc[ vertexID ];
	VSOutput output;

	output.Color0			=	prt.Color1;

	output.Size0			=	prt.Size0;
	
	output.TotalLifeTime	=	prt.TotalLifeTime;
	output.LifeTime			=	prt.LifeTime;

	output.Position			=	float4(prt.Position, 0, 1);

	return output;
}*/


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



[maxvertexcount(6)]
void GSMain( point VSOutput inputPoint[1], inout TriangleStream<GSOutput> outputStream )
{
	GSOutput p0, p1, p2, p3;
	
//	VSOutput prt = inputPoint[0];

	PARTICLE3D prt = GSResourceBuffer[ inputPoint[0].vertexID ];
	
	if (prt.LifeTime >= prt.TotalLifeTime ) {
		return;
	}
	

		float factor = saturate(prt.LifeTime / prt.TotalLifeTime);

//		float sz = lerp( prt.Size0, prt.Size1, factor )/2;

		float sz = prt.Size0;

		float time = prt.LifeTime;

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



float4 PSMain( GSOutput input ) : SV_Target
{
	return Texture.Sample( Sampler, input.TexCoord ) * float4(input.Color.rgb,1);
}