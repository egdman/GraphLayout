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

namespace GraphVis
{
	public class GraphVis : Game
	{
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
			AddService(new ParticleSystem(this), true, true, 9996, 9996);


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

			cam.Config.FreeCamEnabled = true;

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
				var ps = GetService<ParticleSystem>();
				ps.Pause();
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
		
			
			var partSys = GetService<ParticleSystem>();
			
		//	if (InputDevice.IsKeyDown(Keys.Z)) {
		//		Vector2 target = InputDevice.MousePosition;
		//		partSys.AddParticles();
		//		for ( float t=0; t<=1; t+=1.0f/256) {
		//			partSys.AddParticle( new Vector3( 0, 0, 0 ), 9999, 1, 2 );
		//		}
		//	}


			if(InputDevice.IsKeyDown(Keys.X)) {
		//		partSys.AddScaleFreeNetwork();
				partSys.AddBinaryTree();
		//		partSys.AddChain();
			}

			if(InputDevice.IsKeyDown(Keys.Z)) {
				partSys.AddGraphFromFile("../../../../articles_data/idx_edges.txt");
			}

			ds.Add(Color.Orange, "FPS {0}", gameTime.Fps);
			ds.Add("F1   - show developer console");
			ds.Add("F5   - build content and reload textures");
			ds.Add("F12  - make screenshot");
			ds.Add("ESC  - exit");

			base.Update(gameTime);

			//	Update stuff here :
		}





		/// <summary>
		/// Draws game
		/// </summary>
		/// <param name="gameTime"></param>
		/// <param name="stereoEye"></param>
		protected override void Draw(GameTime gameTime, StereoEye stereoEye)
		{
			base.Draw(gameTime, stereoEye);

			//	Draw stuff here :
		}
	}
}
