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
		[Category("General")]
		public int IterationsPerFrame	{ get; set; }
		
		[Category("General")]
		public float StepSize			{ get; set; }
		

		[Category("Physics")]
		public float RepulsionForce		{ get; set; }
		[Category("Physics")]
		public float SpringTension		{ get; set; }


		[Category("Visuals")]
		public float EdgeOpacity
		{
			get { return linkOpacity; }
			set
			{
				if (value < 0) { linkOpacity = 0; }
				else if (value > 1) { linkOpacity = 1; }
				else { linkOpacity = value; }
			}
		}
		[Category("Visuals")]
		public float NodeScale
		{
			get { return nodeScale; }
			set
			{
				if (value < 0) { nodeScale = 0; }
				else { nodeScale = value; }
			}
		}

		[Category("Advanced")]
		public int SearchIterations { get; set; }
		[Category("Advanced")]
		public int SwitchToManualAfter { get; set; }
		[Category("Advanced")]
		public bool UseGPU { get; set; }
		[Category("Advanced")]
		public LayoutSystem.StepMethod StepMode { get; set; }
		[Category("Advanced")]
		public float C1 { get; set; }
		[Category("Advanced")]
		public float C2 { get; set; }

		float linkOpacity;
		float nodeScale;

		public ParticleConfig()
		{
			// General:
			IterationsPerFrame	= 20;
			StepSize			= 0.02f;
			
			// Visuals:
			EdgeOpacity			= 0.1f;
			nodeScale			= 1.0f;

			// Physics constants:
			RepulsionForce	= 1.0f;
			SpringTension	= 0.1f;

			// Advanced defaults:
			StepMode = LayoutSystem.StepMethod.Fixed;
			SwitchToManualAfter	= 250;
			SearchIterations	= 1;	
			C1 = 0.1f;
			C2 = 0.9f;
			UseGPU		= true;	
			
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
		float		edgeSize;


		StructuredBuffer	selectedNodesBuffer; // list of indices of highlighted nodes
		StructuredBuffer	selectedEdgesBuffer; // list of indices of highlighted edges

		ConstantBuffer		paramsCB;
		List<List<int> >	edgeIndexLists;
		List<Link>			edgeList;
		List<Particle3d>	nodeList;
		Queue<int>			commandQueue;
		Random				rand = new Random();

		LayoutSystem		lay;

		int		numSelectedNodes;
		int		numSelectedEdges;
		int		referenceNodeIndex;


		enum RenderFlags {
			DRAW			= 0x1,
			POINT			= 0x1 << 1,
			LINE			= 0x1 << 2,
			SELECTION		= 0x1 << 3,
			HIGH_LINE		= 0x1 << 4,
		}


		[StructLayout(LayoutKind.Explicit, Size = 144)]
		struct Params {
			[FieldOffset(  0)] public Matrix	View;
			[FieldOffset( 64)] public Matrix	Projection;
			[FieldOffset(128)] public int		MaxParticles;
			[FieldOffset(132)] public int		SelectedNode;
			[FieldOffset(136)] public float		edgeOpacity;
			[FieldOffset(140)] public float		nodeScale;
		} 

		/// <summary>
		/// 
		/// </summary>
		/// <param name="game"></param>
		public GraphSystem ( Game game ) : base (game)
		{
			Config = new ParticleConfig();
		}

		public int NodeCount { get { return nodeList.Count; } }
		public int EdgeCount { get { return edgeList.Count; } }


		
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
			edgeSize			=	1000.0f;
			edgeList			=	new List<Link>();
			nodeList		=	new List<Particle3d>();
			edgeIndexLists		=	new List<List<int> >();
			commandQueue		=	new Queue<int>();

			numSelectedNodes	=	0;
			numSelectedEdges	=	0;
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



		public bool ClickNode(Point cursor, StereoEye eye, float threshold, out Vector3 nearestPos, out int nodeIndex )
		{
			nearestPos.X = 0;
			nearestPos.Y = 0;
			nearestPos.Z = 0;
			nodeIndex = 0;

			var cam = Game.GetService<GreatCircleCamera>();
			var viewMatrix = cam.GetViewMatrix( eye );
			var projMatrix = cam.GetProjectionMatrix( eye );
			Graph graph = this.GetGraph();

			Vector2 cursorProj = PixelsToProj(cursor);
			bool nearestFound = false;
			
			float minZ = 99999;
			int currentIndex = 0;
			foreach (SpatialNode node in graph.Nodes)
			{
				Vector4 posWorld = new Vector4(node.Position - ((SpatialNode)graph.Nodes[referenceNodeIndex]).Position, 1.0f);
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

			var cam = Game.GetService<GreatCircleCamera>();
			var viewMatrix = cam.GetViewMatrix(eye);
			var projMatrix = cam.GetProjectionMatrix(eye);

			Graph graph = this.GetGraph();
			int currentIndex = 0;
			foreach (SpatialNode node in graph.Nodes)
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
			if (selectedNodesBuffer != null)
			{
				selectedNodesBuffer.Dispose();
			}
			if (selectedEdgesBuffer != null)
			{
				selectedEdgesBuffer.Dispose();
			}
			List<int> selEdges = new List<int>(); 
			foreach (var ind in nodeIndices)
			{
				foreach (var l in edgeIndexLists[ind])
				{
					selEdges.Add(l);
				}
			}
			selectedNodesBuffer = new StructuredBuffer(Game.GraphicsDevice, typeof(int), nodeIndices.Count, StructuredBufferFlags.Counter);
			selectedEdgesBuffer = new StructuredBuffer(Game.GraphicsDevice, typeof(int), selEdges.Count, StructuredBufferFlags.Counter);
			selectedNodesBuffer.SetData(nodeIndices.ToArray());
			selectedEdgesBuffer.SetData(selEdges.ToArray());
			numSelectedNodes = nodeIndices.Count;
			numSelectedEdges = selEdges.Count;
		}

		public void Deselect()
		{
			numSelectedNodes = 0;
			numSelectedEdges = 0;
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


		public void AddGraph(Graph graph)
		{
			lay.ResetState();
			nodeList.Clear();
			edgeList.Clear();
			edgeIndexLists.Clear();
			referenceNodeIndex = 0;
			setBuffers( graph );
		}


		public void UpdateGraph(Graph graph)
		{
			nodeList.Clear();
			edgeList.Clear();
			edgeIndexLists.Clear();
			setBuffers(graph);
		}


		void addParticle( Vector3 pos, float size, Vector4 color, float colorBoost = 1 )
		{
			nodeList.Add( new Particle3d {
					Position		=	pos,
					Velocity		=	Vector3.Zero,
					Color			=	color * colorBoost,
					Size			=	size,
					Force			=	Vector3.Zero,
					Mass			=	particleMass,
					Charge			=	Config.RepulsionForce
				}
			);
			edgeIndexLists.Add( new List<int>() );
		}


		void addEdge( int end1, int end2 )
		{
			int edgeNumber = edgeList.Count;
			edgeList.Add( new Link{
					par1 = (uint)end1,
					par2 = (uint)end2,
					length = 1.0f,
				}
			);
			edgeIndexLists[end1].Add(edgeNumber);

			edgeIndexLists[end2].Add(edgeNumber);

			// modify particles sizes according to number of edges:
	//		Particle3d newPrt1 = ParticleList[end1];
	//		Particle3d newPrt2 = ParticleList[end2];
	//		newPrt1.Size	+= 0.1f;
	//		newPrt2.Size	+= 0.1f;
	//		ParticleList[end1] = newPrt1;
	//		ParticleList[end2] = newPrt2;
	//		stretchLinks(end1);
	//		stretchLinks(end2);

		}



		public Graph GetGraph()
		{
			if (lay.CurrentStateBuffer != null)
			{
				Particle3d[] particleArray = new Particle3d[lay.ParticleCount];
				lay.CurrentStateBuffer.GetData(particleArray);
				Graph graph = new Graph();
				foreach (var p in particleArray)
				{
					graph.AddNode(new SpatialNode(p.Position, p.Size, new Color(p.Color)));
				}
				foreach (var l in edgeList)
				{
					graph.AddEdge((int)l.par1, (int)l.par2);
				}
				return graph;
			}
			return new Graph();
		}



		void addNode(float size, Color color)
		{
			var zeroV = new Vector3(0, 0, 0);
			addParticle(
					zeroV + RadialRandomVector() * edgeSize,
					size, color.ToVector4(), 1.0f );
		}


		void addNode(float size, Color color, Vector3 position)
		{
			addParticle( position, size, color.ToVector4(), 1.0f );
		}



		void setBuffers(Graph graph)
		{
			foreach (BaseNode n in graph.Nodes)
			{
				if (n is SpatialNode)
				{
					addNode(n.GetSize(), n.GetColor(), ((SpatialNode)n).Position);
				}
				else
				{
					addNode(n.GetSize(), n.GetColor());
				}
			}
			foreach (var e in graph.Edges)
			{
				addEdge( e.End1, e.End2 );
			}
			setBuffers();
		}

		

		void setBuffers()
		{
			lay.SetData(nodeList, edgeList, edgeIndexLists);
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
				if (lay != null)
				{
					lay.Dispose();
				}
			}		
			base.Dispose( disposing );
		}

		void disposeOfBuffers()
		{
			if (selectedNodesBuffer != null)
			{
				selectedNodesBuffer.Dispose();
			}
			if (selectedEdgesBuffer != null)
			{
				selectedEdgesBuffer.Dispose();
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
			var cam = Game.GetService<GreatCircleCamera>();
			
			int lastCommand = 0;
			if ( commandQueue.Count > 0 )
			{
				lastCommand = commandQueue.Dequeue();
			}

			// Calculate positions: ----------------------------------------------------
			lay.UseGPU			= Config.UseGPU;
			lay.SpringTension	= Config.SpringTension;
			lay.StepMode		= Config.StepMode;
			lay.Update(lastCommand);

			// Render: -----------------------------------------------------------------
			Params param = new Params();

			param.View			= cam.GetViewMatrix(stereoEye);
			param.Projection	= cam.GetProjectionMatrix(stereoEye);
			param.SelectedNode	= referenceNodeIndex;

			render( device, lay, param );
			
			// Debug output: ------------------------------------------------------------
			var debStr = Game.GetService<DebugStrings>();
			debStr.Add( Color.Yellow, "drawing " + nodeList.Count + " points" );
			debStr.Add( Color.Yellow, "drawing " + edgeList.Count + " lines" );
			debStr.Add( Color.Black, lay.UseGPU ? "Using GPU" : "Not using GPU" );
			base.Draw( gameTime, stereoEye );
		}


		void render( GraphicsDevice device, LayoutSystem ls, Params parameters )
		{
			parameters.MaxParticles	= lay.ParticleCount;
			parameters.edgeOpacity	= Config.EdgeOpacity;
			parameters.nodeScale	= Config.NodeScale;

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
			device.GeometryShaderResources	[2] = ls.CurrentStateBuffer;
			device.Draw( nodeList.Count, 0 );
						
			// draw lines: -------------------------------------------------------------------------
			device.PipelineState = factory[(int)RenderFlags.DRAW|(int)RenderFlags.LINE];
			device.GeometryShaderResources	[2] = ls.CurrentStateBuffer;
			device.GeometryShaderResources	[3] = ls.LinksBuffer;
			device.Draw( edgeList.Count, 0 );

			// draw selected points: ---------------------------------------------------------------
			device.PipelineState = factory[(int)RenderFlags.DRAW | (int)RenderFlags.SELECTION];
			device.PixelShaderResources		[1] = selectionTex;
			device.GeometryShaderResources	[4] = selectedNodesBuffer;
			device.Draw(numSelectedNodes, 0);

			// draw selected lines: ----------------------------------------------------------------
			device.PipelineState = factory[(int)RenderFlags.DRAW | (int)RenderFlags.HIGH_LINE];
			device.GeometryShaderResources	[5] = selectedEdgesBuffer;
			device.Draw(numSelectedEdges, 0);
		}
	}
}
