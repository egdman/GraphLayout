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
using Fusion.Input;

namespace GraphVis {
	public class ParticleConfig
	{
	}

	public class ParticleSystem : GameService {

		[Config]
		public ParticleConfig cfg{ get; set; }
		public const float WorldRaduis = 50.0f;

		Texture2D		texture;
		Ubershader		shader;
		StateFactory	factory;
		State			state;

		const int	BlockSize				=	256;
		const int	MaxSimulatedParticles	=	1024;

		float		particleMass;
		float		linkSize;
		float		particleSize;

		StructuredBuffer	currentStateBuffer;
		StructuredBuffer	nextStateBuffer;
		StructuredBuffer	linksPtrBuffer;
		StructuredBuffer	enegryBuffer;
		StructuredBuffer	linksBuffer;
		ConstantBuffer		paramsCB;
		Particle3d[]		particleBufferCPU;
		LinkId[]			linksPtrBufferCPU;
		Link[]				linksBufferCPU;
		List<List<int> >	linkPtrLists;
		List<Link>			linkList;
		List<Particle3d>	ParticleList;
		Queue<int>			commandQueue;
		Random rand = new Random();

		float	stepLength;
		float	energy;
		float	deltaEnergy;
		float	pGradE;
		uint	numIterations;
		bool	ignoreConditions;

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
			[FieldOffset(0)]
			public uint par1;
			[FieldOffset(4)]
			public uint par2;
			[FieldOffset(8)]
			public float length;
			[FieldOffset(12)]
			public float force2;
			[FieldOffset(16)]
			public Vector3 orientation;
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
			[FieldOffset(132)] public float		StepLength;
			[FieldOffset(136)] public float		LinkSize;
		} 

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
	//			plState.BlendState		= BlendState.Additive;
				plState.BlendState = BlendState.NegMultiply;
				plState.DepthStencilState = DepthStencilState.Readonly;
				plState.Primitive		= Primitive.PointList;
			} );

			paramsCB			=	new ConstantBuffer( Game.GraphicsDevice, typeof(Params) );
			particleMass		=	0.05f;
			linkSize			=	100.0f;
			particleSize		=	10.0f;
			linkList			=	new List<Link>();
			ParticleList		=	new List<Particle3d>();
			linkPtrLists		=	new List<List<int> >();
			commandQueue		=	new Queue<int>();
			state				=	State.PAUSE;
			stepLength			=	1.0f;
			numIterations		=	0;
			ignoreConditions	=	false;
			Game.InputDevice.KeyDown += keyboardHandler;

			base.Initialize();
		}


		public void Pause()
		{
			if ( state == State.RUN ) {	state = State.PAUSE; }
			else { state = State.RUN; }
		}


		public void SwitchConditionCheck()
		{
			ignoreConditions = !ignoreConditions;
		}


		void keyboardHandler(object sender, Fusion.Input.InputDevice.KeyEventArgs e)
		{
			if ( e.Key == Keys.OemPlus )
			{
				commandQueue.Enqueue(1);
			}
			if ( e.Key == Keys.OemMinus )
			{
				commandQueue.Enqueue(-1);
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

		public void AddGraph(Graph<BaseNode> graph)
		{
			ParticleList.Clear();
			linkList.Clear();
			linkPtrLists.Clear();
			setBuffers( graph );
		}
		
		void addParticle( Vector3 pos, float lifeTime, float size0, Vector4 color, float colorBoost = 1 )
		{
			ParticleList.Add( new Particle3d {
					Position		=	pos,
					Velocity		=	Vector3.Zero,
	//				Color			=	rand.NextVector4( Vector4.Zero, Vector4.One ) * colorBoost,
					Color			=	color * colorBoost,
					Size			=	size0,
					Force			=	Vector3.Zero,
					Mass			=	particleMass,
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
					length = 1.0f,
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

		void addNode(float size, Color color)
		{
			var zeroV = new Vector3(0, 0, 0);
			addParticle(
					zeroV + RadialRandomVector() * linkSize, 9999,
					size, color.ToVector4(), 1.0f );
		}


		void addNodes(int N)
		{
			for (int i = 0; i < N; ++i)
			{
				addNode( particleSize, rand.NextColor() );
			}
		}


		//void addChain( int N, bool linked )
		//{
		//	Vector3 pos = new Vector3( 0, 0, -400);	
		//	for ( int i = 0; i < N; ++i ) {			
		//		addParticle( pos, 9999, particleSize, 1.0f );
		//		pos += RadialRandomVector() * linkSize;
		//	}
		//	if ( linked ) {
		//		for ( int i = 1; i < N; ++i ) {
		//			addLink(i - 1, i);
		//		}
		//	}
		//}


		void setBuffers(Graph<BaseNode> graph)
		{
			foreach (INode n in graph.Nodes)
			{
				addNode(n.GetSize(), n.GetColor());
			}
			foreach (var e in graph.Edges)
			{
				addLink( e.End1, e.End2 );
			}
			setBuffers();
		}


		void setBuffers()
		{
			particleBufferCPU = ParticleList.ToArray();
			linksBufferCPU = linkList.ToArray();
			linksPtrBufferCPU = new LinkId[linkList.Count * 2];
			int iter = 0;
			int lpIter = 0;
			foreach( var ptrList in linkPtrLists ) {

				int blockSize = 0;
				particleBufferCPU[iter].linksPtr = lpIter;
				if (ptrList != null) {
					foreach ( var linkPtr in ptrList ) {
						linksPtrBufferCPU[lpIter] = new LinkId {id = linkPtr};
						++lpIter;
						++blockSize;
					}
				}
				particleBufferCPU[iter].linksCount = blockSize;
				++iter;
			}
		
			disposeOfBuffers();
			
			if ( particleBufferCPU.Length != 0 ) {
				currentStateBuffer	= new StructuredBuffer( Game.GraphicsDevice, typeof(Particle3d),	particleBufferCPU.Length, StructuredBufferFlags.Counter );
				nextStateBuffer		= new StructuredBuffer( Game.GraphicsDevice, typeof(Particle3d),	particleBufferCPU.Length, StructuredBufferFlags.Counter );
				currentStateBuffer.SetData(particleBufferCPU);
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

			state = State.PAUSE;
			stepLength = 0.1f;
			numIterations = 0;

			initCalculations();
		}


	
		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose ( bool disposing )
		{
			if (disposing) {
				paramsCB.Dispose();
				disposeOfBuffers();
				if ( factory != null ) {
					factory.Dispose();
				}	
				if ( texture != null ) {
					texture.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		void disposeOfBuffers()
		{
			if (currentStateBuffer != null)
			{
				currentStateBuffer.Dispose();
			}

			if (nextStateBuffer != null)
			{
				nextStateBuffer.Dispose();
			}

			if (linksBuffer != null)
			{
				linksBuffer.Dispose();
			}

			if (linksPtrBuffer != null)
			{
				linksPtrBuffer.Dispose();
			}

			if (enegryBuffer != null)
			{
				enegryBuffer.Dispose();
			}
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


		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
		/// <param name="stereoEye"></param>
		public override void Draw ( GameTime gameTime, Fusion.Graphics.StereoEye stereoEye )
		{
			var device	=	Game.GraphicsDevice;
			var cam = Game.GetService<OrbitCamera>();
	//		var cam = Game.GetService<Camera>();
			bool cond1 = false;
			bool cond2 = false;

	//		float energyThreshold = 0.003f;
	//		float energyThreshold = 0.1f;
	//		float energyThreshold = (float)ParticleList.Count / 10000.0f;
			float energyThreshold = 0;

			
			int lastCommand = 0;
			if ( commandQueue.Count > 0 )
			{
				lastCommand = commandQueue.Dequeue();
			}
			
			float chosenStepLength = 0;

			// Wolfe constants:
	//		float C1 = 0.1f;
	//		float C2 = 0.99f;

			float C1 = 0.3f;
			float C2 = 0.99f;

//			stepLength = 0.1f;

			// Algorithm outline:
			//
			//  1. Calc descent vector pk
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

			Params param = new Params();

			param.View			=	cam.GetViewMatrix( stereoEye );
			param.Projection	=	cam.GetProjectionMatrix( stereoEye );
			param.MaxParticles	=	0;

			param.LinkSize		=	linkSize;

			if ( currentStateBuffer != null ) {
						
				param.MaxParticles	=	MaxSimulatedParticles;
				float changeFactor = 0.1f;

				if (lastCommand > 0)
				{
					stepLength *= (1.0f + changeFactor);
				}
				if (lastCommand < 0)
				{
					stepLength /= (1.0f + changeFactor);
				}


				if ( state == State.RUN ) {
					for ( int i = 0; i < 50; ++i )
			//		do
					{
						float Ek	= energy;
						float Ek1	= 0;

						float pkGradEk	= 0;
						float pkGradEk1	= 0;

						cond1 = false;
						cond2 = false;

						int tries = 0;

						if (!ignoreConditions)
						{
							calcTotalEnergyAndDotProduct(device, currentStateBuffer, currentStateBuffer,
									enegryBuffer, param, out Ek, out pkGradEk);
						}

						while ( !(cond1 && cond2) )
						{
							// FOR TESTING: //
							if (ignoreConditions)
							{
								cond1 = true;
								cond2 = true;
							}

							param.StepLength = stepLength;

							moveVertices( device, currentStateBuffer, nextStateBuffer, param );
							calcDescentVector(device, nextStateBuffer, param); // calc energies

							if (!ignoreConditions)
							{
								calcTotalEnergyAndDotProduct(device, currentStateBuffer, nextStateBuffer,
										enegryBuffer, param, out Ek1, out pkGradEk1);


								// check Wolfe conditions:
								cond1 = (Ek1 - Ek <= stepLength * C1 * pkGradEk);
								cond2 = (pkGradEk1 >= C2 * pkGradEk);

								if (tries > 4)
								{
									// Debug output:
									Console.WriteLine("step = " + stepLength + " " +
										"cond#1 = " + (cond1 ? "TRUE" : "FALSE") + " " +
										"cond#2 = " + (cond2 ? "TRUE" : "FALSE") + " " +
										"deltaE = " + (Ek1 - Ek)
										);
								}

								// change step length factor:
								if (cond1 && !cond2) { stepLength *= (1.0f + changeFactor); }
								if (!cond1 && !cond2) { stepLength *= (1.0f + changeFactor); }

								if (!cond1 && cond2 && Ek1 < Ek) { stepLength /= (1.0f + changeFactor); }
								if (!cond1 && cond2 && Ek1 >= Ek) { stepLength /= (1.0f + changeFactor); }
							}
							++tries;
							++numIterations;

							// Temporary way to prevent freeze:
							if ( tries > 5 ) break;
						}

				
						// swap buffers: --------------------------------------------------------------------
						var temp = currentStateBuffer;
						currentStateBuffer = nextStateBuffer;
						nextStateBuffer = temp;
						chosenStepLength = stepLength;
						// Reset step length factor:
				//		stepLength = 1.0f;
				//		stepLength = 10.0f;
						
						energy = Ek1;
						deltaEnergy = Ek1 - Ek;
						pGradE = pkGradEk1;
						if (Math.Abs(deltaEnergy) < energyThreshold)
						{
							state = State.PAUSE;
							break;
						}
					}
		//			while (true);
				}
			}


			// Render: ------------------------------------------------------------------
			render( device, param );
			
			// Debug output: ------------------------------------------------------------
			var debStr = Game.GetService<DebugStrings>();
			debStr.Add( Color.Yellow, "drawing " + ParticleList.Count + " points" );
			debStr.Add( Color.Yellow, "drawing " + linkList.Count + " lines" );
			debStr.Add( Color.Aqua, "Step factor  = " + chosenStepLength );
			debStr.Add( Color.Aqua, "Energy           = " + energy );
			debStr.Add( Color.Aqua, "DeltaEnergy      = " + deltaEnergy );
			debStr.Add( Color.Aqua, "pTp              = " + pGradE );
			debStr.Add( Color.Aqua, "Iteration        = " + numIterations );
			base.Draw( gameTime, stereoEye );
		}

		


		void render( GraphicsDevice device, Params parameters )
		{
				device.ResetStates();
			device.ClearBackbuffer( Color.White );
				device.SetTargets( null, device.BackbufferColor );
				paramsCB.SetData(parameters);

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
						
				// draw lines: -------------------------------------------------------------------------
				device.PipelineState = factory[(int)Flags.DRAW|(int)Flags.LINE];
				device.GeometryShaderResources[1] = currentStateBuffer;
				device.GeometryShaderResources[3] = linksBuffer;
				device.Draw( linkList.Count, 0 );		
		}


		/// <summary>
		/// This function performss the first iteration of calculations
		/// </summary>
		void initCalculations()
		{
			Params param = new Params();
			param.MaxParticles = MaxSimulatedParticles;

			var device = Game.GraphicsDevice;
			calcDescentVector(device, currentStateBuffer, param); // calc desc vector and energies
			calcTotalEnergyAndDotProduct(device, currentStateBuffer, currentStateBuffer,
					enegryBuffer, param, out energy, out pGradE);
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
			energy /= 2; // because each pair is counted 2 times
		}
	}
}
