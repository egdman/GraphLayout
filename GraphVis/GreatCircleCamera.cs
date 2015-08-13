using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Fusion;
using Fusion.Graphics;
using Fusion.Audio;
using Fusion.Input;
using Fusion.Content;
using Fusion.Development;
using Fusion.Mathematics;

namespace GraphVis
{
	class GreatCircleCamera : Camera
	{
		Vector3 radial;  // points at camera from the center
		Vector3 centerAbs; // global position of the center
		Vector3 up; // points up

		float angularVelocity;
		float upDownVelocity;

		float altitude;

		const float PI180 = (float)Math.PI / 180.0f;

		public float ZeroRadius
		{
			get;
			set;
		}

		public float FOV
		{
			get;
			set;
		}



		public Vector3 CenterOfOrbit
		{
			get { return centerAbs; }
			set { centerAbs = value; }
		}

		public GreatCircleCamera( Game game ) : base(game)
		{
			centerAbs = new Vector3(0, 0, 0);
			radial = new Vector3();
			up = new Vector3();
			FOV = 70.0f;
		}

		public override void Initialize()
		{
			base.Initialize();
			ZeroRadius = 50.0f;
			altitude	= 1000.0f;
			radial.X = 1;
			radial.Y = radial.Z = 0;

			up.X = up.Y = 0;
			up.Z = 1;
		}


		public override void Update(GameTime gameTime)
		{
			Config.FreeCamEnabled = false;
			var ds = Game.GetService<DebugStrings>();

			angularVelocity = 0.15f * PI180;
			upDownVelocity = 0.0007f * altitude;

			if (Game.InputDevice.IsKeyDown(Keys.LeftShift))
			{
				angularVelocity *= 3;
				upDownVelocity *= 3;
			}

			Vector3 axis = new Vector3(0, 0, 0);
			Vector3 side = Vector3.Cross(up, radial);
			if (Game.InputDevice.IsKeyDown(Keys.W))
			{
				axis += -side;
			}
			if (Game.InputDevice.IsKeyDown(Keys.S))
			{
				axis += side;
			}
			if (Game.InputDevice.IsKeyDown(Keys.A))
			{
				axis += -up;
			}
			if (Game.InputDevice.IsKeyDown(Keys.D))
			{
				axis += up;
			}

			if (Game.InputDevice.IsKeyDown(Keys.Space))
			{
				altitude += upDownVelocity * gameTime.Elapsed.Milliseconds;
			}

			if (Game.InputDevice.IsKeyDown(Keys.C))
			{
				altitude -= upDownVelocity * gameTime.Elapsed.Milliseconds;
			}

			if (altitude < 0.0f)
			{
				altitude = 0.0f;
			}
			if (axis.Length() > 0)
			{		
				float angle = angularVelocity * gameTime.Elapsed.Milliseconds;
				Matrix3x3 rot = Matrix3x3.RotationAxis(Vector3.Normalize(axis), angle);

				radial = Vector3.Transform(radial, rot);
				up = Vector3.Transform(up, rot);
			}

			Vector3 cameraLocation = CenterOfOrbit + (ZeroRadius + altitude) * radial;
			base.SetupCamera(cameraLocation, CenterOfOrbit, up, new Vector3(0, 0, 0),
				PI180 * FOV, base.Config.FreeCamZNear, base.Config.FreeCamZFar, 0, 0);

			//ds.Add( "Altitude = " + altitude + " m" );
			//ds.Add( "Longitude = " + longitude );
			//ds.Add( "Latitude = " + latitude );

			base.Update(gameTime);
		}


		public void DollyZoom(float FOVrate)
		{
			float FOV2 = FOV + FOVrate;
			FOV2 = FOV2 < 2		? 2		: FOV2;
			FOV2 = FOV2 > 150	? 150	: FOV2;
			altitude = (altitude+ZeroRadius) * (float)(Math.Tan(PI180 * FOV/2.0f) / Math.Tan(PI180*FOV2/2.0f)) - ZeroRadius;
			FOV = FOV2;
		}

	}
}
