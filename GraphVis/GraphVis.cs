using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Mathematics;
using Fusion.Graphics;
using Fusion.Audio;
using Fusion.Input;
using Fusion.Content;
using Fusion.Development;
using Fusion.UserInterface;

namespace GraphVis
{
	public class GraphVis : Game
	{
        Random rnd = new Random();
		int selectedNodeIndex;
		Vector3 selectedNodePos;
		bool isSelected;
		Tuple<Point, Point> dragFrame;
		int time;


		/// <summary>
		/// GraphVis constructor
		/// </summary>
		public GraphVis()
			: base()
		{
			//	enable object tracking :
			Parameters.TrackObjects = true;

			//	uncomment to enable debug graphics device:
			//	(MS Platform SDK must be installed)
			//	Parameters.UseDebugDevice	=	true;

			//	add services :
			AddService(new SpriteBatch(this), false, false, 0, 0);
			AddService(new DebugStrings(this), true, true, 9999, 9999);
			AddService(new DebugRender(this), true, true, 9998, 9998);

			//	add here additional services :
			AddService(new Camera(this), true, false, 9997, 9997);
	//		AddService(new OrbitCamera(this), true, false, 9996, 9996 );
			AddService(new GreatCircleCamera(this), true, false, 9995, 9995);
			AddService(new GraphSystem(this), true, true, 9994, 9994);


			//	add here additional services :

			//	load configuration for each service :
			LoadConfiguration();

			//	make configuration saved on exit :
			Exiting += Game_Exiting;
		}


		/// <summary>
		/// Initializes game :
		/// </summary>
		protected override void Initialize()
		{
			//	initialize services :
			base.Initialize();

			var cam = GetService<Camera>();

			cam.Config.FreeCamEnabled = false;
			selectedNodeIndex = 0;
			selectedNodePos = new Vector3();
			isSelected = false;
			time = 0;
			//	add keyboard handler :
			InputDevice.KeyDown += InputDevice_KeyDown;
			//	load content & create graphics and audio resources here:
		}



		/// <summary>
		/// Disposes game
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				//	dispose disposable stuff here
				//	Do NOT dispose objects loaded using ContentManager.
			}
			base.Dispose(disposing);
		}



		/// <summary>
		/// Handle keys
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void InputDevice_KeyDown(object sender, Fusion.Input.InputDevice.KeyEventArgs e)
		{
			if (e.Key == Keys.F1)
			{
				DevCon.Show(this);
			}

			if (e.Key == Keys.F5)
			{
				Reload();
			}

			if (e.Key == Keys.F12)
			{
				GraphicsDevice.Screenshot();
			}

			if (e.Key == Keys.Escape)
			{
				Exit();
			}
			if ( e.Key == Keys.P )
			{
				GetService<GraphSystem>().Pause();
			}
			if (e.Key == Keys.I)
			//{
			//	GetService<GraphSystem>().SwitchStepMode();
			//}
			if (e.Key == Keys.M)
			{
				Graph graph = Graph.MakeTree( 256, 2 );				
				float[] centralities = new float[graph.NodeCount];
				float maxC = graph.GetCentrality(0);
				float minC = maxC;
				for (int i = 0; i < graph.NodeCount; ++i)
				{
					centralities[i] = graph.GetCentrality(i);
					maxC = maxC < centralities[i] ? centralities[i] : maxC;
					minC = minC > centralities[i] ? centralities[i] : minC;
					Log.Message( ":{0}", i );
				}

				float range = maxC - minC;
				for (int i = 0; i < graph.NodeCount; ++i)
				{
					centralities[i] -= minC;
					centralities[i] /= range;
					centralities[i] *= 0.9f;
					centralities[i] += 0.1f;
					var color = new Color(0.6f, 0.3f, centralities[i], 1.0f);
			//		var color = new Color(centralities[i]); // B/W
					graph.Nodes[i] = new BaseNode(5.0f, color);
				}
				GetService<GraphSystem>().AddGraph(graph);
			}
			if (e.Key == Keys.Q)
			{
				Graph graph = GetService<GraphSystem>().GetGraph();
				graph.WriteToFile( "graph.gr" );
				Log.Message( "Graph saved to file" );
			}
			if (e.Key == Keys.F) // focus on a node
			{
				var cam = GetService<GreatCircleCamera>();
				var pSys = GetService<GraphSystem>();
				if (!isSelected)
				{
	//				cam.CenterOfOrbit = new Vector3(0, 0, 0);
				}
				else
				{
	//				cam.CenterOfOrbit = pSys.GetGraph().Nodes[selectedNodeIndex].Position;
					pSys.ChangeReference(selectedNodeIndex);
				}
			}
            if (e.Key == Keys.G) // collapse random edge
            {
                var pSys = GetService<GraphSystem>();
                Graph graph = pSys.GetGraph();
                int edge = rnd.Next(graph.EdgeCount);
				graph.CollapseEdge(edge);
				pSys.UpdateGraph(graph);
            }
			if (e.Key == Keys.LeftButton)
			{
				var pSys = GetService<GraphSystem>();
				Point cursor = InputDevice.MousePosition;
				Vector3 nodePosition = new Vector3();
				int selNode = 0;
				if (pSys.CursorNearestNode(cursor, StereoEye.Mono, 0.025f, out nodePosition, out selNode))
				{
					selectedNodeIndex = selNode;
					isSelected = true;
					//dr.DrawBox(new BoundingBox(-5.0f * Vector3.One, 5.0f * Vector3.One),
					//	Matrix.Translation(nodePosition),
					//	Color.Blue
					//	);
					selectedNodePos = nodePosition;
				}
				else
				{
					isSelected = false;
				}
			}

		}



		/// <summary>
		/// Saves configuration on exit.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void Game_Exiting(object sender, EventArgs e)
		{
			SaveConfiguration();
		}



		/// <summary>
		/// Updates game
		/// </summary>
		/// <param name="gameTime"></param>
		protected override void Update(GameTime gameTime)
		{
			var ds = GetService<DebugStrings>();

			var cam = GetService<Camera>();
			var debRen = GetService<DebugRender>();
					
			var graphSys = GetService<GraphSystem>();

			if(InputDevice.IsKeyDown(Keys.X)) {
				Graph graph = Graph.MakeTree( 256, 2 );
		//		Graph<BaseNode> graph = Graph<BaseNode>.MakeRing( 512 );
				graphSys.AddGraph(graph);
			}

			if(InputDevice.IsKeyDown(Keys.Z)) {
				StanfordNetwork graph = new StanfordNetwork();
		//		graph.ReadFromFile("../../../../articles_data/idx_edges.txt");
				graph.ReadFromFile("../../../../collab_networks/CA-GrQc.txt");

				graphSys.AddGraph(graph);
			}

			ds.Add(Color.Orange, "FPS {0}", gameTime.Fps);
			ds.Add(Color.Orange, "F1   - show developer console");
			ds.Add(Color.Orange, "F5   - build content and reload textures");
			ds.Add(Color.Orange, "F12  - make screenshot");
			ds.Add(Color.Orange, "ESC  - exit");
			ds.Add(Color.Orange, "Press Z or X to load graph");
			ds.Add(Color.Orange, "Press M to load painted graph (SLOW!)");
			ds.Add(Color.Orange, "Press P to pause/unpause");
//			ds.Add(Color.Orange, "Press I to switch to manual mode");

			base.Update(gameTime);

		}





		/// <summary>
		/// Draws game
		/// </summary>
		/// <param name="gameTime"></param>
		/// <param name="stereoEye"></param>
		protected override void Draw(GameTime gameTime, StereoEye stereoEye)
		{
			base.Draw(gameTime, stereoEye);

	//		time += gameTime.Elapsed.Milliseconds;

			var cam = GetService<GreatCircleCamera>();
			var dr = GetService<DebugRender>();
			var pSys = GetService<GraphSystem>(); 
			dr.View = cam.GetViewMatrix(stereoEye);
			dr.Projection = cam.GetProjectionMatrix(stereoEye);

			var ds = GetService<DebugStrings>();
			if (isSelected)
			{
				ds.Add(Color.Orange, "Selected node # " + selectedNodeIndex);
				pSys.Select(selectedNodeIndex);
			}
			else
			{
				ds.Add(Color.Orange, "No selection");
				pSys.Deselect();
			}
		}
	}
}
