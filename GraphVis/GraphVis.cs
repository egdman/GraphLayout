using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Audio;
using Fusion.Content;
using Fusion.Graphics;
using Fusion.Input;
using Fusion.Utils;

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
			//	root directory for standard x64 C# application

	

			//	enable object tracking :
			Parameters.TrackObjects = true;

			//	enable debug graphics device in Debug :
#if DEBUG
				Parameters.UseDebugDevice	=	true;
#endif

			//	add services :
			AddService(new SpriteBatch(this), false, false, 0, 0);
			AddService(new DebugStrings(this), true, true, 9999, 9999);
			AddService(new DebugRender(this), true, true, 9998, 9998);
			AddService(new Camera(this), true, false, 9997, 9997);

			//	add here additional services :
			AddService(new ParticleSystem(this), true, true, 9996, 9996);
			//	load configuration for each service :
			LoadConfiguration();

			//	make configuration saved on exit :
			Exiting += FusionGame_Exiting;
		}


		/// <summary>
		/// Add services :
		/// </summary>
		protected override void Initialize()
		{
			//	initialize services :
			base.Initialize();

			var cam = GetService<Camera>();

			cam.Config.FreeCamEnabled = true;
		//	cam.SetPose( new Vector3(-30, 0, 0), 0, 0, 0 );

			
			
			//	add keyboard handler :
			InputDevice.KeyDown += InputDevice_KeyDown;
		}



		/// <summary>
		/// Handle keys for each demo
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void InputDevice_KeyDown(object sender, Fusion.Input.InputDevice.KeyEventArgs e)
		{
			if (e.Key == Keys.F1)
			{
	//			ShowEditor();
			}

			if (e.Key == Keys.F5)
			{
	//			BuildContent();
	//			Content.Reload<Texture2D>();
			}

			if (e.Key == Keys.F7)
			{
	//			BuildContent();
				Content.ReloadDescriptors();
			}

			if (e.Key == Keys.F12)
			{
				GraphicsDevice.Screenshot();
			}

			if (e.Key == Keys.Escape)
			{
				Exit();
			}
			if (e.Key == Keys.Q)
			{
				var ps = GetService<ParticleSystem>();
				ps.Pause();
			}
		}



		/// <summary>
		/// Save configuration on exit.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void FusionGame_Exiting(object sender, EventArgs e)
		{
			SaveConfiguration();
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
		protected override void Update(GameTime gameTime)
		{
			var ds = GetService<DebugStrings>();

			var cam = GetService<Camera>();
			var debRen = GetService<DebugRender>();

			int	w	=	GraphicsDevice.Viewport.Width;
			int h	=	GraphicsDevice.Viewport.Height;

			debRen.View			= cam.ViewMatrix;
			debRen.Projection	= cam.ProjMatrix;
	
	//		debRen.DrawGrid(10);

	//		debRen.DrawBox( 
	//			new BoundingBox(
	//				new Vector3(-10.0f, -10.0f, -10.0f),
	//				new Vector3( 10.0f,  10.0f,  10.0f)
	//			), 
	//			Color.White
	//		);
			
			var partSys = GetService<ParticleSystem>();
			
			if (InputDevice.IsKeyDown(Keys.Z)) {
		//		Vector2 target = InputDevice.MousePosition;
				partSys.AddMaxParticles();
		//		for ( float t=0; t<=1; t+=1.0f/256) {
		//			partSys.AddParticle( new Vector3( 0, 0, 0 ), 9999, 1, 2 );
		//		}
			}

			if(InputDevice.IsKeyDown(Keys.X)) {
				partSys.AddScaleFreeNetwork();
			}

			ds.Add(Color.Orange, "FPS {0}", gameTime.Fps);
			ds.Add("F1   - show developer console");
			ds.Add("F5   - build content and reload textures");
			ds.Add("F12  - make screenshot");
			ds.Add("ESC  - exit");
	//		ds.Add("Vector3 = " + System.Runtime.InteropServices.Marshal.SizeOf(new float()).ToString() );

	//		ds.Add(cam.ViewMatrix.Row1.ToString());
	//		ds.Add(cam.ViewMatrix.Row2.ToString());
	//		ds.Add(cam.ViewMatrix.Row3.ToString());
	//		ds.Add(cam.ViewMatrix.Row4.ToString());
			
			base.Update(gameTime);
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
		/// <param name="stereoEye"></param>
		protected override void Draw(GameTime gameTime, StereoEye stereoEye)
		{
			base.Draw(gameTime, stereoEye);
		}
	}
}
