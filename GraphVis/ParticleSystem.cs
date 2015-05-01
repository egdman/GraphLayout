using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;

using Fusion;
using Fusion.Graphics;
using Fusion.Mathematics;



namespace GraphVis {

	public enum IntegratorType
	{
		EULER,		
		RUNGE_KUTTA
	}

	public class ParticleConfig
	{
		
		float maxParticleMass;
		float minParticleMass;
		float rotation;
		IntegratorType iType;

		[Category("Particle mass")]
		[Description("Largest particle mass")]
		public float Max_mass { get{ return  maxParticleMass; } set{ maxParticleMass = value; } }

		[Category("Particle mass")]
		[Description("Smallest particle mass")]
		public float Min_mass { get{ return  minParticleMass; } set{ minParticleMass = value; } }

		[Category("Integrator type")]
		[Description("Integrator type")]
		public IntegratorType IType{ get{ return iType; } set{ iType = value; } }

		[Category("Initial rotation")]
		[Description("Rate of initial rotation")]
		public float Rotation { get{ return  rotation; } set{ rotation = value; } }

		public ParticleConfig()
		{
			minParticleMass	= 0.5f;
			maxParticleMass	= 0.5f;
			rotation		= 2.6f;
			iType			= IntegratorType.RUNGE_KUTTA; 
		}
	}



	public class ParticleSystem : GameService {


		[Config]
		public ParticleConfig cfg{ get; set; }

		Texture2D	texture;
		Ubershader	shader;
		StateFactory factory;

		State		state;

		const int	BlockSize				=	256;

		const int	MaxInjectingParticles	=	4096;
		const int	MaxSimulatedParticles	=	MaxInjectingParticles;

		float		MaxParticleMass;
		float		MinParticleMass;
		float		spinRate;
		float		linkSize;
		float		particleSize;

		Particle3d[]		injectionBufferCPU;

		StructuredBuffer	currentStateBuffer;
		StructuredBuffer	nextStateBuffer;
		StructuredBuffer	linksPtrBuffer;
		StructuredBuffer	enegryBuffer;
		LinkId[]			linksPtrBufferCPU;

		StructuredBuffer	linksBuffer;
		Link[]				linksBufferCPU;

		Vector4[]			energyBufferCPU;

		ConstantBuffer		paramsCB;
		List<List<int> >	linkPtrLists;

		List<Link>			linkList;
		List<Particle3d>	ParticleList;

		float maxAcc;
		float maxVelo;
		float stepLength;
		float elapsedTime;
		int	progress;
		float energy;
		float pGradE;

		uint numIterations;

	

		[StructLayout(LayoutKind.Explicit)]
			struct Particle3d {
			[FieldOffset( 0)] public Vector3	Position;
			[FieldOffset(12)] public Vector3	Velocity;
			[FieldOffset(24)] public Vector3	Force;
			[FieldOffset(36)] public float		Energy;
			[FieldOffset(40)] public float		Mass;
			[FieldOffset(44)] public float		Charge;

			[FieldOffset(48)] public Vector4	Color;
			[FieldOffset(64)] public float		Size;
			[FieldOffset(68)] public int		linksPtr;
			[FieldOffset(72)] public int		linksCount;
		}



		// link between 2 particles:
		[StructLayout(LayoutKind.Explicit)]
		struct Link
		{
			[FieldOffset( 0)] public uint par1;
			[FieldOffset( 4)] public uint par2;
			[FieldOffset( 8)] public float length;
			[FieldOffset(12)] public float force2;
			[FieldOffset(16)] public Vector3 orientation;
		}


		[StructLayout(LayoutKind.Explicit)]
		struct LinkId
		{
			[FieldOffset( 0)] public int id;
		}

		enum Flags {
			INJECTION		=	0x1,
			SIMULATION		=	0x1 << 1,
			MOVE			=	0x1 << 2,
			REDUCTION		=	0x1 << 3,
			EULER			=	0x1 << 4,
			RUNGE_KUTTA		=	0x1 << 5,
			
			POINT			=	0x1 << 6,
			LINE			=	0x1 << 7,

			COMPUTE			=	0x1 << 8,
			DRAW			=	0x1 << 9,

			LINKS			=	0x1 << 10
		}

		enum State {
			RUN,
			PAUSE
		}

		[StructLayout(LayoutKind.Explicit, Size = 144)]
		struct Params {
			[FieldOffset(  0)] public Matrix	View;
			[FieldOffset( 64)] public Matrix	Projection;
			[FieldOffset(128)] public uint		MaxParticles;
			[FieldOffset(132)] public float		DeltaTime;
			[FieldOffset(136)] public float		LinkSize;
		} 

		Random rand = new Random();


		/// <summary>
		/// 
		/// </summary>
		/// <param name="game"></param>
		public ParticleSystem ( Game game ) : base (game)
		{
			cfg = new ParticleConfig();
		}


		/// <summary>
		/// 
		/// </summary>
		public override void Initialize ()
		{
			texture		=	Game.Content.Load<Texture2D>("smaller");
			shader		=	Game.Content.Load<Ubershader>("Compute");

			factory = new StateFactory( shader, typeof(Flags), ( plState, comb ) => 
			{
				plState.RasterizerState	= RasterizerState.CullNone;
				plState.BlendState		= BlendState.Additive;
				plState.DepthStencilState = DepthStencilState.Readonly;
				plState.Primitive		= Primitive.PointList;
			} );

			paramsCB			=	new ConstantBuffer( Game.GraphicsDevice, typeof(Params) );
	//		MaxParticleMass		=	cfg.Max_mass;
	//		MinParticleMass		=	cfg.Min_mass;

			MaxParticleMass		=	0.05f;
			MinParticleMass		=	0.05f;

			spinRate			=	cfg.Rotation;
			linkSize			=	1.0f;
			particleSize		=	1.0f;

			linkList			=	new List<Link>();
			ParticleList		=	new List<Particle3d>();
			linkPtrLists		=	new List<List<int> >();
			state				=	State.RUN;

			maxAcc				=	0;
			maxVelo				=	0;
			stepLength		=	1.0f;
			progress			=	0;
			numIterations		=	0;

			base.Initialize();
		}



		public void Pause()
		{
			if ( state == State.RUN ) {
				state = State.PAUSE;
			}
			else {
				state = State.RUN;
			}
		}



		/// <summary>
		/// Returns random radial vector
		/// </summary>
		/// <returns></returns>
		Vector3 RadialRandomVector ()
		{
			Vector3 r;
			do {
				r	=	rand.NextVector3( -Vector3.One, Vector3.One );
			} while ( r.Length() > 1 );

			r.Normalize();

			return r;
		}



		public void AddScaleFreeNetwork( int N = MaxInjectingParticles )
		{
			ParticleList.Clear();
			linkList.Clear();
			linkPtrLists.Clear();
			addChain(5, true);
			while ( ParticleList.Count < N ) {
			//	int id = rand.Next( 0, ParticleList.Count - 1 );
				for ( int id = 0; id < ParticleList.Count; ++id ) {
					int ifJoins = rand.Next( 0, 2 * linkList.Count - 1 );

					if ( ParticleList.Count >= N ) break;

					if ( ifJoins < linkPtrLists[id].Count ) {
						addChildren( 1, id );
					}
				}
			}
			setBuffers();
		}



		public void AddGraphFromFile( string path )
		{
			ParticleList.Clear();
			linkList.Clear();
			linkPtrLists.Clear();
			addChain( 1000, false );
			var dstrings = File.ReadAllLines(path);
            if (dstrings.Length > 0)
            {
                for (int i = 0; i < dstrings.Length; i = i + 2)
                {
                    string citName = dstrings[i];
                    string[] citations;
                    citations = dstrings[i + 1].Split(new Char[] { '\t', ' ', ',' });

                    foreach (string cit in citations)
                    {
                        if (cit != "")
                        {
                            addLink(int.Parse(citName), int.Parse(cit));
                        }
                    }
                }
            }
			setBuffers();
		}


		public void AddBinaryTree( int N = MaxInjectingParticles )
		{
			ParticleList.Clear();
			linkList.Clear();
			linkPtrLists.Clear();
			addChain( 1, false );
			addChildren( 2, ParticleList.Count - 1 );

			Queue<int> latestIndex = new Queue<int>();
			latestIndex.Enqueue( ParticleList.Count - 1 );
			latestIndex.Enqueue( ParticleList.Count - 2 );

			while ( ParticleList.Count < N )
			{
				if ( latestIndex.Count <= 0 )
				{
					break;
				}
				int parentIndex = latestIndex.Peek();

				if ( linkPtrLists[parentIndex].Count > 2 )
				{
					latestIndex.Dequeue();
					continue;
				}
				addChildren(1, parentIndex);
				latestIndex.Enqueue( ParticleList.Count - 1 );
			}
			setBuffers();
		}


		
		void addParticle( Vector3 pos, float lifeTime, float size0, float colorBoost = 1 )
		{
			float ParticleMass	=	rand.NextFloat( MinParticleMass, MaxParticleMass );
			ParticleList.Add( new Particle3d {
					Position		=	pos,
					Velocity		=	Vector3.Zero,
					Color			=	rand.NextVector4( Vector4.Zero, Vector4.One ) * colorBoost,
					Size			=	size0,
					Force			=	Vector3.Zero,
					Mass			=	ParticleMass,
					Charge			=	0.05f
				}
			);
			linkPtrLists.Add( new List<int>() );


		}


		void addLink( int end1, int end2 )
		{
			int linkNumber = linkList.Count;
			linkList.Add( new Link{
					par1 = (uint)end1,
					par2 = (uint)end2,
					length = linkSize,
					force2 = 0,
					orientation = Vector3.Zero
				}
			);
			linkPtrLists[end1].Add(linkNumber);

			linkPtrLists[end2].Add(linkNumber);

			// modify particles masses and sizes according to number of links:
			Particle3d newPrt1 = ParticleList[end1];
			Particle3d newPrt2 = ParticleList[end2];
			newPrt1.Mass	+= 0.7f;
			newPrt2.Mass	+= 0.7f;
			newPrt1.Size	+= 0.1f;
			newPrt2.Size	+= 0.1f;
			ParticleList[end1] = newPrt1;
			ParticleList[end2] = newPrt2;
			stretchLinks(end1);
			stretchLinks(end2);

		}


		void stretchLinks( int particleId )
		{
			var lList = linkPtrLists[particleId];

			foreach ( var link in lList )
			{
				Link modifLink = linkList[link];
 				modifLink.length *= 1.1f;
				linkList[link] = modifLink;
			}
		}



		public void AddChain()
		{
			ParticleList.Clear();
			linkList.Clear();
			linkPtrLists.Clear();
			addChain( MaxSimulatedParticles, true );
			setBuffers();
		}

		void addChain( int N, bool linked )
		{
			Vector3 pos = new Vector3( 0, 0, -400);	
			for ( int i = 0; i < N; ++i ) {			
				addParticle( pos, 9999, particleSize, 1.0f );
				pos += RadialRandomVector() * linkSize;
			}
			if ( linked ) {
				for ( int i = 1; i < N; ++i ) {
					addLink(i - 1, i);
				}
			}
		}



		void addChildren( int howMany, int parentId )
		{
			Vector3 parentPos = new Vector3( ParticleList[parentId].Position.X,
							ParticleList[parentId].Position.Y,
							ParticleList[parentId].Position.Z );

			int newParticleId = ParticleList.Count;
			for ( int i = 0; i < howMany; ++i ) {
				addParticle( parentPos + RadialRandomVector() * linkSize,
					9999,
					particleSize,
					1.0f );
				addLink( parentId, newParticleId );
				++newParticleId;
			}

		}


		int countSumOfDegrees()
		{
			int sum = 0;
			foreach ( var ls in linkPtrLists ) {
				sum += ls.Count;
			}
			return sum;
		}


		void setBuffers()
		{
			injectionBufferCPU = new Particle3d[ParticleList.Count];
			int iter = 0;
			foreach( var p in ParticleList ) {
				injectionBufferCPU[iter] = p;
				++iter;
			}
			linksBufferCPU = new Link[linkList.Count];
			iter = 0;
			foreach ( var l in linkList ) {
				linksBufferCPU[iter] = l;
				++iter;
			}

			linksPtrBufferCPU = new LinkId[linkList.Count * 2];
			iter = 0;
			int lpIter = 0;
			foreach( var ptrList in linkPtrLists ) {

				int blockSize = 0;
				injectionBufferCPU[iter].linksPtr = lpIter;
				if (ptrList != null) {
					foreach ( var linkPtr in ptrList ) {
						linksPtrBufferCPU[lpIter] = new LinkId {id = linkPtr};
						++lpIter;
						++blockSize;
					}
				}
				injectionBufferCPU[iter].linksCount = blockSize;
				++iter;
			}
		
			if ( currentStateBuffer != null ) {
				currentStateBuffer.Dispose();
			}

			if ( nextStateBuffer != null ) {
				nextStateBuffer.Dispose();
			}

			if ( linksBuffer != null ) {
				linksBuffer.Dispose();
			}

			if ( linksPtrBuffer != null ) {
				linksPtrBuffer.Dispose();
			}

			if ( enegryBuffer != null ) {
				enegryBuffer.Dispose();
			}
			

			if ( injectionBufferCPU.Length != 0 ) {
				currentStateBuffer	= new StructuredBuffer( Game.GraphicsDevice, typeof(Particle3d),	injectionBufferCPU.Length, StructuredBufferFlags.Counter );
				nextStateBuffer		= new StructuredBuffer( Game.GraphicsDevice, typeof(Particle3d),	injectionBufferCPU.Length, StructuredBufferFlags.Counter );
				currentStateBuffer.SetData(injectionBufferCPU);
				enegryBuffer = new StructuredBuffer (
							Game.GraphicsDevice,
							typeof(Vector4),
							MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ),
							StructuredBufferFlags.Counter );
			}
			if ( linksBufferCPU.Length != 0 ) {
				linksBuffer	= new StructuredBuffer(
							Game.GraphicsDevice,
							typeof(Link),
							linksBufferCPU.Length,
							StructuredBufferFlags.Counter );
				linksBuffer.SetData(linksBufferCPU);
			}
			if ( linksPtrBufferCPU.Length != 0 ) {
				linksPtrBuffer = new StructuredBuffer( 
							Game.GraphicsDevice,
							typeof(LinkId),
							linksPtrBufferCPU.Length,
							StructuredBufferFlags.Counter );
				linksPtrBuffer.SetData(linksPtrBufferCPU);
			}

			state = State.RUN;
			maxAcc = 0;
			maxVelo	= 0;
			stepLength = 0.1f;
			elapsedTime = 0;
			progress = 0;
			numIterations = 0;
		}


	


		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose ( bool disposing )
		{
			if (disposing) {
				paramsCB.Dispose();

				if ( currentStateBuffer != null ) {
					currentStateBuffer.Dispose();
				}

				if ( nextStateBuffer != null ) {
					nextStateBuffer.Dispose();
				}
				if ( linksBuffer != null ) {
					linksBuffer.Dispose();
				}

				if ( linksPtrBuffer != null ) {
					linksPtrBuffer.Dispose();
				}

				if ( factory != null ) {
					factory.Dispose();
				}

				if ( enegryBuffer != null ) {
					enegryBuffer.Dispose();
				}
				if ( texture != null ) {
					texture.Dispose();
				}
			}
			base.Dispose( disposing );
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
		public override void Update ( GameTime gameTime )
		{
			base.Update( gameTime );

			var ds = Game.GetService<DebugStrings>();

		//	ds.Add( Color.Yellow, "Total particles DST: {0}", simulationBufferDst.GetStructureCount() );
		//	ds.Add( Color.Yellow, "Total particles SRC: {0}", simulationBufferSrc.GetStructureCount() );
		//	ds.Add( Color.Yellow, "Injection count: {0}", injectionCount );
		//	ds.Add( Color.Yellow, "Particle array length: {0}", injectionBufferCPU == null ? "null" : injectionBufferCPU.Length.ToString() );
		}

		/// <summary>
		/// 
		/// </summary>
		void SwapParticleBuffers ()
		{
			var temp = nextStateBuffer;
			nextStateBuffer = currentStateBuffer;
			currentStateBuffer = temp;
		}


		void calcExtremeValues( Particle3d [] buffer)
		{
			float maxAcceleration	= 0;
			float maxVelocity		= 0;
			foreach ( var p in buffer )
			{
				float velo = p.Velocity.Length();
				maxVelocity		= velo > maxVelocity ? velo : maxVelocity;
			}

			maxAcc = maxAcceleration;
			maxVelo = maxVelocity;
			energy = 0;
			pGradE	= 0;
			foreach ( var en in energyBufferCPU )
			{
				energy	+= en.X;
				pGradE	+= en.Y;
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
		/// <param name="stereoEye"></param>
		public override void Draw ( GameTime gameTime, Fusion.Graphics.StereoEye stereoEye )
		{
			var device	=	Game.GraphicsDevice;
			var cam = Game.GetService<Camera>();
			float deltaEnergy = 0;

			//  1. Calc desc dir pk
			//  2. calc pk * grad(Ek)
			//  3. calc Ek
			//  4. try move with some step factor
			//  5. calc pk * grad(Ek+1)
 			//  6. calc Ek+1
			//  7. check Wolfe conditions
			//  8. if both are OK GOTO 11
			//  9. modify step factor
			// 10. GOTO 4
			// 11. swap try buffer with current buffer
			// 12. GOTO 1
			// 



			if ( currentStateBuffer != null ) {
				
				
				Params param = new Params();

				param.View			=	cam.GetViewMatrix( stereoEye );
				param.Projection	=	cam.GetProjectionMatrix( stereoEye );
				param.MaxParticles	=	0;
	//			param.DeltaTime		=	gameTime.ElapsedSec*timeStepFactor;
				param.DeltaTime		=	stepLength;
				param.LinkSize		=	linkSize;


				device.ComputeShaderConstants	[0] = paramsCB;
				device.VertexShaderConstants	[0] = paramsCB;
				device.GeometryShaderConstants	[0] = paramsCB;
				device.PixelShaderConstants		[0] = paramsCB;
			
				//	Simulate : ------------------------------------------------------------------------
				//

				param.MaxParticles	=	MaxSimulatedParticles;
				paramsCB.SetData( param );

//				StreamWriter writer = File.AppendText( "../../../energyVsStepNum.csv" );

				if ( state == State.RUN ) {
					for ( int i = 0; i < 1; ++i )
					{

						param.DeltaTime = stepLength;
						paramsCB.SetData( param );

						// calculate forces and energies: ---------------------------------------------------
						device.SetCSRWBuffer( 0, currentStateBuffer, MaxSimulatedParticles );
						device.ComputeShaderResources[2] = linksPtrBuffer;
						device.ComputeShaderResources[3] = linksBuffer;
						device.ComputeShaderConstants[0] = paramsCB;

						device.PipelineState = factory[(int)Flags.COMPUTE|(int)Flags.SIMULATION|(int)Flags.EULER|(int)Flags.LINKS];

						device.Dispatch( MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );//*/

						// move particles: ------------------------------------------------------------
						device.SetCSRWBuffer( 0, null );
						device.ComputeShaderResources[1] = currentStateBuffer;
						device.SetCSRWBuffer( 0, nextStateBuffer, MaxSimulatedParticles );

						device.PipelineState = factory[(int)Flags.COMPUTE|(int)Flags.MOVE];
						device.Dispatch( MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );//*/

						elapsedTime += param.DeltaTime;
						++numIterations;

		//			}

#if true
						// calculate energies in thread blocks:
						device.SetCSRWBuffer( 0, null ); // unbind from UAV
						device.ComputeShaderResources[1] = nextStateBuffer; // bind to SRV
						device.SetCSRWBuffer( 1, enegryBuffer, MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );

						device.PipelineState = factory[(int)Flags.COMPUTE|(int)Flags.REDUCTION];
						device.Dispatch( MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );
#endif
						
						// Swap buffers: ----------------------------
						var tmp = currentStateBuffer;
						currentStateBuffer = nextStateBuffer;
						nextStateBuffer = tmp;
						// ------------------------------------------
					
						//////////////////////////////
						float stepChangeCoef = 0.9f;
			//			float stepChangeCoef = 1.0f;
						//////////////////////////////

				
						if ( energyBufferCPU == null ) {
							energyBufferCPU = new Vector4[enegryBuffer.GetStructureCount()];
						}

						enegryBuffer.GetData( energyBufferCPU );

						if ( injectionBufferCPU == null ) {
							injectionBufferCPU = new Particle3d[currentStateBuffer.GetStructureCount()];
						}
			
						float prevEnergy = energy;
						
						if ( injectionBufferCPU.Length > 0 ) {
							calcExtremeValues(injectionBufferCPU);
						}

						deltaEnergy = energy - prevEnergy;


						// ------------------------------------------------------------------------------------
#if true
						if ( deltaEnergy > 0.01f )
						{
							progress = 0;
						}
						else
						{
							++progress;
						}

						if ( progress >= 4 )
						{
							stepLength /= stepChangeCoef;
							progress = 0;
						}
						else if ( progress == 0 )
						{
							if ( stepLength < 0.1f )
							{
								stepLength = 0.1f;
							}
							else
							{
								stepLength *= stepChangeCoef;
							}
						}

				
						// TERMINATION CONDITION CHECK --------------------------------------------------------
			//			if ( energy < 0.00002f ) {
			//				state = State.PAUSE;
			//			}
			//			writer.WriteLine( numIterations + "," + energy );
#endif
#if true			
						if ( deltaEnergy <= pGradE * 0.001f * stepLength ) {
							stepLength /= stepChangeCoef;
						}
						else {
							stepLength *= stepChangeCoef;
						}
#endif
					}
				
	
				}


	//			if ( maxAcc > 0.1f ) {
			//		timeStepFactor = 0.5f / maxAcc;
	//				timeStepFactor = 20f / maxAcc;
	//			}

//				writer.Close();
				// ------------------------------------------------------------------------------------
				device.ResetStates();
				device.SetTargets( null, device.BackbufferColor );
				//	Render: ---------------------------------------------------------------------------
				//
				device.ComputeShaderConstants	[0] = paramsCB;
				device.VertexShaderConstants	[0] = paramsCB;
				device.GeometryShaderConstants	[0] = paramsCB;
				device.PixelShaderConstants		[0] = paramsCB;

				device.PixelShaderSamplers		[0] = SamplerState.LinearWrap;
			
				// draw points: ------------------------------------------------------------------------
				device.PipelineState = factory[(int)Flags.DRAW|(int)Flags.POINT];
				device.SetCSRWBuffer( 0, null );
				device.PixelShaderResources[0] = texture;
				device.GeometryShaderResources[1] = currentStateBuffer;
				device.Draw( ParticleList.Count, 0 );
						
				// draw lines: --------------------------------------------------------------------------
				device.PipelineState = factory[(int)Flags.DRAW|(int)Flags.LINE];
				device.GeometryShaderResources[1] = currentStateBuffer;
				device.GeometryShaderResources[3] = linksBuffer;
				device.Draw( linkList.Count, 0 );		
			}
			// --------------------------------------------------------------------------------------
			
			var debStr = Game.GetService<DebugStrings>();

			debStr.Add("Press Z to start simulation");
			debStr.Add("Press Q to pause/unpause");
			debStr.Add( Color.Yellow, "drawing " + ParticleList.Count + " points" );
			debStr.Add( Color.Yellow, "drawing " + linkList.Count + " lines" );
			debStr.Add( Color.Aqua, "Max acceleration = " + maxAcc );
			debStr.Add( Color.Aqua, "TimeStep factor  = " + stepLength );
			debStr.Add( Color.Aqua, "Energy           = " + energy );
			debStr.Add( Color.Aqua, "DeltaEnergy      = " + deltaEnergy );
			debStr.Add( Color.Aqua, "pTp              = " + pGradE );
			debStr.Add( Color.Aqua, "Iteration        = " + numIterations );
			if ( deltaEnergy <= pGradE * 0.99f * stepLength ) {
				debStr.Add( Color.Aqua, "Condition #1:  TRUE" );	
			} else {
				debStr.Add( Color.Aqua, "Condition #1:  FALSE" );
			}

			base.Draw( gameTime, stereoEye );
		}
		

		/// <summary>
		/// This function calculates two values:
		/// 1. Energy E for every vertex				(N floats)
		/// 2. Descent vector which equals -grad(E)		(3*N floats)
		/// Values are overwritten into the same buffer
		/// </summary>
		/// <param name="device"></param>
		/// <param name="rwVertexBuffer"></param>
		/// <param name="parameters"></param>
		void calcDescentVector( GraphicsDevice device, StructuredBuffer rwVertexBuffer, Params parameters )
		{
			paramsCB.SetData( parameters );
			device.ComputeShaderConstants[0] = paramsCB;
			device.SetCSRWBuffer( 0, rwVertexBuffer, MaxSimulatedParticles );
			device.ComputeShaderResources[2] = linksPtrBuffer;
			device.ComputeShaderResources[3] = linksBuffer;
			device.PipelineState = factory[(int)(Flags.COMPUTE|Flags.SIMULATION|Flags.EULER|Flags.LINKS)];
			device.Dispatch( MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );
			device.ResetStates();
		}



		/// <summary>
		/// This function takes vertices from the source buffer and moves them by
		/// descent vector times stepLength (Pk*stepLength).
		/// Then it writes them into the destination buffer with new positions.
		/// This function does not change data in the source buffer.
		/// </summary>
		/// <param name="device"></param>
		/// <param name="srcVertexBuffer"></param>
		/// <param name="dstVertexBuffer"></param>
		/// <param name="parameters"></param>
		void moveVertices( GraphicsDevice device, StructuredBuffer srcVertexBuffer,
							StructuredBuffer dstVertexBuffer, Params parameters )
		{
			paramsCB.SetData( parameters );
			device.ComputeShaderConstants[0] = paramsCB;
			device.ComputeShaderResources[1] = srcVertexBuffer;
			device.SetCSRWBuffer( 0, dstVertexBuffer, MaxSimulatedParticles );
			device.PipelineState = factory[(int)(Flags.COMPUTE|Flags.MOVE)];
			device.Dispatch( MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );
			device.ResetStates();
		}



		/// <summary>
		/// This function calculates the following values:
		/// 1. Total energy at (k+1)th iteration (from nextStateBuffer).
		/// 2. Dot product of kth descent vector and (k+1)th energy gradient (which equals minus (k+1)th descent vector)
		/// </summary>
		/// <param name="device"></param>
		/// <param name="currentStateBuffer"></param>
		/// <param name="nextStateBuffer"></param>
		/// <param name="outputValues"></param>
		/// <param name="parameters"></param>
		/// <param name="energy"></param>
		/// <param name="pTgradE"></param>
		void calcTotalEnergyAndDotProduct( GraphicsDevice device, StructuredBuffer currentStateBuffer,
			StructuredBuffer nextStateBuffer, StructuredBuffer outputValues, Params parameters,
			out float energy, out float pTgradE )
		{
			// preform reduction on GPU:
			paramsCB.SetData( parameters );
			device.ComputeShaderConstants[0] = paramsCB;
			device.ComputeShaderResources[1] = currentStateBuffer;
			device.ComputeShaderResources[4] = nextStateBuffer;
			device.SetCSRWBuffer( 1, outputValues, MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );
			device.PipelineState = factory[(int)Flags.COMPUTE|(int)Flags.REDUCTION];
			device.Dispatch( MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );

			// perform final summation:
			Vector4[] valueBufferCPU = new Vector4[outputValues.GetStructureCount()];
			outputValues.GetData( valueBufferCPU );
			energy	= 0;
			pTgradE	= 0;
			foreach( var value in valueBufferCPU )
			{
				energy	+= value.X;
				pTgradE	+= value.Y;
			}
		}


	}
}
