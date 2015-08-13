#if 0
$ubershader	DRAW POINT|LINE|SELECTION|HIGH_LINE
#endif

struct PARAMS {
	float4x4	View;
	float4x4	Projection;
	int			MaxParticles;
	int			SelectedParticle;
	float		edgeOpacity;
	float		nodeScale;
	float4		nodeColor;
	float4		edgeColor;
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
};

SamplerState					Sampler				: 	register(s0);

Texture2D						Texture 			: 	register(t0);
Texture2D						SelectionTexture	:	register(t1);
StructuredBuffer<PARTICLE3D>	particleReadBuffer	:	register(t2);
StructuredBuffer<Link>			linksBuffer			:	register(t3);
StructuredBuffer<int>			SelectedNodeIndices	:	register(t4);
StructuredBuffer<int>			SelectedLinkIndices	:	register(t5);


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

	PARTICLE3D prt = particleReadBuffer[ inputPoint[0].vertexID ];
	PARTICLE3D referencePrt = particleReadBuffer[ Params.SelectedParticle ];

	float sz = prt.Size0 * Params.nodeScale;
	float4 color	=	prt.Color0;
	float4 pos		=	float4( prt.Position.xyz - referencePrt.Position.xyz, 1 );
	float4 posV		=	mul( pos, Params.View );

	p0.Position = mul( posV + float4( sz, sz, 0, 0 ) , Params.Projection );		
	p0.TexCoord = float2(1,1);
	p0.Color = color;

	p1.Position = mul( posV + float4(-sz, sz, 0, 0 ) , Params.Projection );
	p1.TexCoord = float2(0,1);
	p1.Color = color;

	p2.Position = mul( posV + float4(-sz,-sz, 0, 0 ) , Params.Projection );
	p2.TexCoord = float2(0,0);
	p2.Color = color;

	p3.Position = mul( posV + float4( sz,-sz, 0, 0 ) , Params.Projection );
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
	PARTICLE3D referencePrt = particleReadBuffer[ Params.SelectedParticle ];

	float4 pos1 = float4( end1.Position.xyz - referencePrt.Position.xyz, 1 );
	float4 pos2 = float4( end2.Position.xyz - referencePrt.Position.xyz, 1 );

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

	float c			=	Params.edgeOpacity;
	p1.Color		=	float4(c,c,c,0);
	p2.Color		=	float4(c,c,c,0);

	outputStream.Append(p1);
	outputStream.Append(p2);
	outputStream.RestartStrip(); 


}

#endif // LINE

#ifdef SELECTION

[maxvertexcount(8)]
void GSMain( point VSOutput inputPoint[1], inout TriangleStream<GSOutput> outputStream )
{
	GSOutput p0, p1, p2, p3;

	PARTICLE3D prt = particleReadBuffer[ SelectedNodeIndices[inputPoint[0].vertexID] ];
	PARTICLE3D referencePrt = particleReadBuffer[ Params.SelectedParticle ];

	float sz = prt.Size0*1.5f*Params.nodeScale;
//	float4 color	=	float4(0, 1, 0, 1);
	float4 color	=	Params.nodeColor;
	float4 pos		=	float4( prt.Position.xyz - referencePrt.Position.xyz, 1 );
	float4 posV		=	mul( pos, Params.View );

	p0.Position = mul( posV + float4( sz, sz, 0, 0 ) , Params.Projection );	
	p0.TexCoord = float2(1,1);
	p0.Color = color;

	p1.Position = mul( posV + float4(-sz, sz, 0, 0 ) , Params.Projection );
	p1.TexCoord = float2(0,1);
	p1.Color = color;

	p2.Position = mul( posV + float4(-sz,-sz, 0, 0 ) , Params.Projection );
	p2.TexCoord = float2(0,0);
	p2.Color = color;

	p3.Position = mul( posV + float4( sz,-sz, 0, 0 ) , Params.Projection );
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

#endif // SELECTION




// Draw highlighted links: --------------------------------------------------------------------
#ifdef HIGH_LINE
[maxvertexcount(2)]
void GSMain( point VSOutput inputLine[1], inout LineStream<GSOutput> outputStream )
{
	GSOutput p1, p2;

	Link lk = linksBuffer[ SelectedLinkIndices[inputLine[0].vertexID] ];
	PARTICLE3D end1 = particleReadBuffer[ lk.par1 ];
	PARTICLE3D end2 = particleReadBuffer[ lk.par2 ];
	PARTICLE3D referencePrt = particleReadBuffer[ Params.SelectedParticle ];

	float4 pos1 = float4( end1.Position.xyz - referencePrt.Position.xyz, 1 );
	float4 pos2 = float4( end2.Position.xyz - referencePrt.Position.xyz, 1 );

	float4 posV1	=	mul( pos1, Params.View );
	float4 posV2	=	mul( pos2, Params.View );

	p1.Position		=	mul( posV1, Params.Projection );
	p2.Position		=	mul( posV2, Params.Projection );

	p1.TexCoord		=	float2(0, 0);
	p2.TexCoord		=	float2(0, 0);

	p1.Color		=	Params.edgeColor;
	p2.Color		=	Params.edgeColor;

	outputStream.Append(p1);
	outputStream.Append(p2);
	outputStream.RestartStrip(); 

}




#endif // HIGH_LINE


#if defined(LINE) || defined(HIGH_LINE)
float4 PSMain( GSOutput input ) : SV_Target
{
	return float4(input.Color.rgb,1);
}
#endif // LINE || HIGH_LINE



#ifdef POINT
float4 PSMain( GSOutput input ) : SV_Target
{
	return Texture.Sample( Sampler, input.TexCoord ) * float4(input.Color.rgb,1);
}
#endif // POINT


#ifdef SELECTION
float4 PSMain( GSOutput input ) : SV_Target
{
	return SelectionTexture.Sample( Sampler, input.TexCoord ) * float4(input.Color.rgb,1);
}
#endif // SELECTION


#endif //DRAW