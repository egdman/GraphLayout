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
		[Category("Advanced")]
		public int	SearchIterations	{ get; set; }
		[Category("Advanced")]
		public int	SwitchToManualAfter	{ get; set; }
		[Category("Advanced")]
		public bool	UseGPU				{ get; set; }
		[Category("Advanced")]
		public LayoutSystem.StepMethod StepMode	{ get; set; }

		[Category("Simple")]
		public int IterationsPerFrame	{ get; set; }
		[Category("Simple")]
		public float RepulsionForce		{ get; set; }
		[Category("Simple")]
		public float StepSize			{ get; set; }

		public ParticleConfig()
		{
			IterationsPerFrame	= 20;
			SearchIterations	= 5;
			SwitchToManualAfter = 250;
			UseGPU			= true;
			StepMode		= LayoutSystem.StepMethod.Fixed;
			RepulsionForce	= 0.05f;
			StepSize		= 0.5f;
		}

	}

	public class GraphSystem : GameService {

		[Config]
		public ParticleConfig Config{ get; set; }
//		public const float WorldSize = 50.0f;

		Texture2D		particleTex;
		Texture2D		selectionTex;
		Ubershader		renderShader;
		Ubershader		computeShader;
		StateFactory	factory;

		float		particleMass;
		float		linkSize;


		StructuredBuffer	selectedIndicesBuffer;
		ConstantBuffer		paramsCB;
		List<List<int> >	linkIndexLists;
		List<Link>			linkList;
		List<Particle3d>	ParticleList;
		Queue<int>			commandQueue;
		Random				rand = new Random();

		LayoutSystem		lay;

		int		numSelectedNodes;
		int		referenceNodeIndex;


		enum RenderFlags {
			DRAW			= 0x1,
			POINT			= 0x1 << 1,
			LINE			= 0x1 << 2,
			SELECTION		= 0x1 << 3,
		}


		[StructLayout(LayoutKind.Explicit, Size = 144)]
		struct Params {
			[FieldOffset(  0)] public Matrix	View;
			[FieldOffset( 64)] public Matrix	Projection;
			[FieldOffset(128)] public int		MaxParticles;
			[FieldOffset(132)] public int		SelectedNode;
		} 

		/// <summary>
		/// 
		/// </summary>
		/// <param name="game"></param>
		public GraphSystem ( Game game ) : base (game)
		{
			Config = new ParticleConfig();
		}

		/// <summary>
		/// 
		/// </summary>
		public override void Initialize ()
		{
			particleTex		=	Game.Content.Load<Texture2D>("smaller");
			selectionTex	=	Game.Content.Load<Texture2D>("selection");
			renderShader	=	Game.Content.Load<Ubershader>("Render");
			computeShader	=	Game.Content.Load<Ubershader>("Compute");

			// creating the layout system:
			lay = new LayoutSystem(Game, computeShader);
			lay.UseGPU = Config.UseGPU;
			lay.RunPause = LayoutSystem.State.PAUSE;

			factory = new StateFactory( renderShader, typeof(RenderFlags), ( plState, comb ) => 
			{
				plState.RasterizerState	= RasterizerState.CullNone;
				plState.BlendState = BlendState.NegMultiply;
				plState.DepthStencilState = DepthStencilState.Readonly;
				plState.Primitive		= Primitive.PointList;
			} );

			paramsCB			=	new ConstantBuffer( Game.GraphicsDevice, typeof(Params) );
			particleMass		=	1.0f;
			linkSize			=	100.0f;
			linkList			=	new List<Link>();
			ParticleList		=	new List<Particle3d>();
			linkIndexLists		=	new List<List<int> >();
			commandQueue		=	new Queue<int>();

			numSelectedNodes	=	0;
			referenceNodeIndex	=	0;

			Game.InputDevice.KeyDown += keyboardHandler;

			base.Initialize();
		}


		public void Pause()
		{
			lay.Pause();
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


		Vector2 PixelsToProj(Point point)
		{
			Vector2 proj = new Vector2(
				(float)point.X / (float)Game.GraphicsDevice.DisplayBounds.Width,
				(float)point.Y / (float)Game.GraphicsDevice.DisplayBounds.Height
			);
			proj.X = proj.X * 2 - 1;
			proj.Y = -proj.Y * 2 + 1;
			return proj;
		}



		public bool CursorNearestNode(Point cursor, StereoEye eye, float threshold, out Vector3 nearestPos, out int nodeIndex )
		{
			nearestPos.X = 0;
			nearestPos.Y = 0;
			nearestPos.Z = 0;
			nodeIndex = 0;

			var cam = Game.GetService<OrbitCamera>();
			var viewMatrix = cam.GetViewMatrix( eye );
			var projMatrix = cam.GetProjectionMatrix( eye );
			Graph<SpatialNode> graph = this.GetGraph();

			Vector2 cursorProj = PixelsToProj(cursor);
			bool nearestFound = false;
			
			float minZ = 99999;
			int currentIndex = 0;
			foreach (var node in graph.Nodes)
			{
				Vector4 posWorld = new Vector4(node.Position - graph.Nodes[referenceNodeIndex].Position, 1.0f);
				Vector4 posView = Vector4.Transform(posWorld, viewMatrix);
				Vector4 posProj = Vector4.Transform(posView, projMatrix);
				posProj /= posProj.W;
				Vector2 diff = new Vector2(posProj.X - cursorProj.X, posProj.Y - cursorProj.Y);
				if (diff.Length() < threshold)
				{
					nearestFound = true;
					if (minZ > posProj.Z)
					{
						minZ = posProj.Z;
						nearestPos.X = posWorld.X;
						nearestPos.Y = posWorld.Y;
						nearestPos.Z = posWorld.Z;
						nodeIndex = currentIndex;
					}
				}
				++currentIndex;
			}
			return nearestFound;
		}


		public List<int> DragSelect(Point topLeft, Point bottomRight, StereoEye eye )
		{
			List<int> selectedIndices = new List<int>();
			Vector2 topLeftProj		= PixelsToProj(topLeft);
			Vector2 bottomRightProj	= PixelsToProj(bottomRight);

			var cam = Game.GetService<OrbitCamera>();
			var viewMatrix = cam.GetViewMatrix(eye);
			var projMatrix = cam.GetProjectionMatrix(eye);

			Graph<SpatialNode> graph = this.GetGraph();
			int currentIndex = 0;
			foreach (var node in graph.Nodes)
			{
				Vector4 posWorld = new Vector4(node.Position, 1.0f);
				Vector4 posView = Vector4.Transform(posWorld, viewMatrix);
				Vector4 posProj = Vector4.Transform(posView, projMatrix);
				posProj /= posProj.W;
				if
				(	posProj.X >= topLeftProj.X && posProj.X <= bottomRightProj.X &&
					posProj.Y >= bottomRightProj.Y && posProj.Y <= topLeftProj.Y
				)
				{
					selectedIndices.Add(currentIndex);
				}
				++currentIndex;
			}
			return selectedIndices;
		}




		public void Select(int nodeIndex)
		{
			Select( new int[1] {nodeIndex} );
		}


		public void Select(ICollection<int> nodeIndices)
		{
			if (selectedIndicesBuffer != null)
			{
				selectedIndicesBuffer.Dispose();
			}
			
			selectedIndicesBuffer = new StructuredBuffer(Game.GraphicsDevice, typeof(int), nodeIndices.Count, StructuredBufferFlags.Counter);
			selectedIndicesBuffer.SetData(nodeIndices.ToArray());
			numSelectedNodes = nodeIndices.Count;
		}

		public void Deselect()
		{
			numSelectedNodes = 0;
		}


		public void ChangeReference(int nodeIndex)
		{
			referenceNodeIndex = nodeIndex;
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
			lay.ResetState();
			ParticleList.Clear();
			linkList.Clear();
			linkIndexLists.Clear();
			setBuffers( graph );
		}


		public void AddGraph(Graph<SpatialNode> graph)
		{
			lay.ResetState();
			ParticleList.Clear();
			linkList.Clear();
			linkIndexLists.Clear();
			setBuffers(graph);
		}


		public void UpdateGraph(Graph<SpatialNode> graph)
		{
			ParticleList.Clear();
			linkList.Clear();
			linkIndexLists.Clear();
			setBuffers(graph);
		}


		void addParticle( Vector3 pos, float size, Vector4 color, float colorBoost = 1 )
		{
			ParticleList.Add( new Particle3d {
					Position		=	pos,
					Velocity		=	Vector3.Zero,
					Color			=	color * colorBoost,
					Size			=	size,
					Force			=	Vector3.Zero,
					Mass			=	particleMass,
					Charge			=	Config.RepulsionForce
				}
			);
			linkIndexLists.Add( new List<int>() );
		}


		void addLink( int end1, int end2 )
		{
			int linkNumber = linkList.Count;
			linkList.Add( new Link{
					par1 = (uint)end1,
					par2 = (uint)end2,
					length = 1.0f,
				}
			);
			linkIndexLists[end1].Add(linkNumber);

			linkIndexLists[end2].Add(linkNumber);

			// modify particles sizes according to number of links:
			Particle3d newPrt1 = ParticleList[end1];
			Particle3d newPrt2 = ParticleList[end2];
	//		newPrt1.Size	+= 0.1f;
	//		newPrt2.Size	+= 0.1f;
			ParticleList[end1] = newPrt1;
			ParticleList[end2] = newPrt2;
			stretchLinks(end1);
			stretchLinks(end2);

		}


		void stretchLinks( int particleId )
		{
			var lList = linkIndexLists[particleId];

			foreach ( var link in lList )
			{
				Link modifLink = linkList[link];
				modifLink.length *= 1.1f;
				linkList[link] = modifLink;
			}
		}


		public Graph<SpatialNode> GetGraph()
		{
			if (lay.CurrentStateBuffer != null)
			{
				Particle3d[] particleArray = new Particle3d[lay.ParticleCount];
				lay.CurrentStateBuffer.GetData(particleArray);
				Graph<SpatialNode> graph = new Graph<SpatialNode>();
				foreach (var p in particleArray)
				{
					graph.AddNode(new SpatialNode(p.Position, p.Size, new Color(p.Color)));
				}
				foreach (var l in linkList)
				{
					graph.AddEdge((int)l.par1, (int)l.par2);
				}
				return graph;
			}
			return new Graph<SpatialNode>();
		}



		void addNode(float size, Color color)
		{
			var zeroV = new Vector3(0, 0, 0);
			addParticle(
					zeroV + RadialRandomVector() * linkSize * 10.0f,
					size, color.ToVector4(), 1.0f );
		}


		void addNode(float size, Color color, Vector3 position)
		{
			addParticle( position, size, color.ToVector4(), 1.0f );
		}



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

		void setBuffers(Graph<SpatialNode> graph)
		{
			foreach (SpatialNode n in graph.Nodes)
			{
				addNode(n.GetSize(), n.GetColor(), n.Position);
			}
			foreach (var e in graph.Edges)
			{
				addLink(e.End1, e.End2);
			}
			setBuffers();
		}


		void setBuffers()
		{
			lay.SetData(ParticleList, linkList, linkIndexLists);
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
				if ( particleTex != null ) {
					particleTex.Dispose();
				}
				if (renderShader != null)
				{
					renderShader.Dispose();
				}
				if (computeShader != null)
				{
					computeShader.Dispose();
				}
			}
			lay.Dispose();
			base.Dispose( disposing );
		}

		void disposeOfBuffers()
		{
			if (selectedIndicesBuffer != null)
			{
				selectedIndicesBuffer.Dispose();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
		public override void Update ( GameTime gameTime )
		{
			base.Update( gameTime );
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
			
			int lastCommand = 0;
			if ( commandQueue.Count > 0 )
			{
				lastCommand = commandQueue.Dequeue();
			}

			// Calculate positions: ----------------------------------------------------
			lay.Update(lastCommand);

			// Render: -----------------------------------------------------------------
			Params param = new Params();

			param.View			= cam.GetViewMatrix(stereoEye);
			param.Projection	= cam.GetProjectionMatrix(stereoEye);
			param.MaxParticles = lay.ParticleCount;
			param.SelectedNode	= referenceNodeIndex;

			render( device, param );
			
			// Debug output: ------------------------------------------------------------
			var debStr = Game.GetService<DebugStrings>();
			debStr.Add( Color.Yellow, "drawing " + ParticleList.Count + " points" );
			debStr.Add( Color.Yellow, "drawing " + linkList.Count + " lines" );
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
			device.PipelineState = factory[(int)RenderFlags.DRAW|(int)RenderFlags.POINT];
			device.SetCSRWBuffer( 0, null );
			device.PixelShaderResources		[0] = particleTex;
			device.GeometryShaderResources	[2] = lay.CurrentStateBuffer;
			device.Draw( ParticleList.Count, 0 );
						
			// draw lines: -------------------------------------------------------------------------
			device.PipelineState = factory[(int)RenderFlags.DRAW|(int)RenderFlags.LINE];
			device.GeometryShaderResources	[2] = lay.CurrentStateBuffer;
			device.GeometryShaderResources	[3] = lay.LinksBuffer;
			device.Draw( linkList.Count, 0 );

			// draw selected points: ---------------------------------------------------------------
			device.PipelineState = factory[(int)RenderFlags.DRAW | (int)RenderFlags.SELECTION];
			device.PixelShaderResources		[1] = selectionTex;
			device.GeometryShaderResources	[4] = selectedIndicesBuffer;
			device.Draw(numSelectedNodes, 0);
		}
	}
}
