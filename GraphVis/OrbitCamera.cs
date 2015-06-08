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
	class OrbitCamera : Camera
	{

		float latitude;
		float longitude;
		float altitude;

		float angularVelocity;
		float latVelocity;
		float lonVelocity;
		float upDownVelocity;

		float zeroRadius;

		const float PI180		= (float)Math.PI / 180;

		public float Altitude
		{
			get { return altitude; }
		}
		public float Latitude
		{
			get { return latitude; }
		}
		public float Longitude
		{
			get { return longitude; }
		}


		public OrbitCamera( Game game ) : base(game)
		{
			
		}


		public override void Initialize()
		{

			base.Initialize();
			
			var rndr = Game.GetService<ParticleSystem>();

			zeroRadius		= ParticleSystem.WorldRaduis;
			latitude	= 0;
			longitude	= 0;
			altitude	= 5.0f;

			angularVelocity = 0.005f;
			latVelocity		= 0.005f;
			lonVelocity		= 0.005f;
			upDownVelocity	= 1;

			
		}


		public override void Update(GameTime gameTime)
		{		
			Config.FreeCamEnabled	= false;

			var rndr	= Game.GetService<ParticleSystem>();
			var ds		= Game.GetService<DebugStrings>();

			angularVelocity	= 0.25f;
			upDownVelocity	= 0.0007f * altitude;

			if ( Game.InputDevice.IsKeyDown( Keys.LeftShift ) ) {
				angularVelocity *= 3;
				upDownVelocity	*= 3;
			}

			latVelocity = angularVelocity;
			lonVelocity = angularVelocity;

			if ( Game.InputDevice.IsKeyDown( Keys.W ) ) {
				latitude += latVelocity * gameTime.Elapsed.Milliseconds;
			}
			if ( Game.InputDevice.IsKeyDown( Keys.S ) ) {
				latitude -= latVelocity * gameTime.Elapsed.Milliseconds;
			}
			if ( Game.InputDevice.IsKeyDown( Keys.A ) ) {
				longitude -= lonVelocity * gameTime.Elapsed.Milliseconds;
			}
			if ( Game.InputDevice.IsKeyDown( Keys.D ) ) {
				longitude += lonVelocity * gameTime.Elapsed.Milliseconds;
			}

			if ( Game.InputDevice.IsKeyDown( Keys.Space ) ) {
				altitude += upDownVelocity * gameTime.Elapsed.Milliseconds;
			}

			if ( Game.InputDevice.IsKeyDown( Keys.C ) ) {
				altitude -= upDownVelocity * gameTime.Elapsed.Milliseconds;
			}

			if ( altitude < 0.0f ) {
				altitude = 0.0f;
			}



			Vector3 cameraLocation = anglesToCoords( latitude, longitude, (zeroRadius + altitude) );
//			base.SetPose( cameraLocation, 0, 0, 0 );
//			base.LookAt( cameraLocation, new Vector3(0, 0, 0), new Vector3( 0, 1, 0) );
			base.SetupCamera( cameraLocation, new Vector3(0, 0, 0), new Vector3( 0, 1, 0), new Vector3(0, 0, 0),
				120.0f, base.Config.FreeCamZNear,base.Config.FreeCamZFar, 0, 0 );

			//ds.Add( "Altitude = " + altitude + " m" );
			//ds.Add( "Longitude = " + longitude );
			//ds.Add( "Latitude = " + latitude );
			
			base.Update(gameTime);
		}


		Vector3 anglesToCoords( float lat, float lon, float radius )
		{
			lat = lat * PI180;
			lon = lon * PI180;
			float rSmall	= radius * (float)( Math.Cos( lat ) );
			float X = (float)( rSmall * Math.Sin( lon ) );
 			float Z = (float)( rSmall * Math.Cos( lon ) );
			float Y = radius * (float)( Math.Sin( lat ) );
			return new Vector3( X, Y, Z );
		}



	}
}
