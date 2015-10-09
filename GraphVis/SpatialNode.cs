using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Fusion;
using Fusion.Graphics;
using Fusion.Mathematics;
using Fusion.Input;

namespace GraphVis
{
	public class SpatialNode : BaseNode
	{

		public Vector3 Position { get; set; }

		public SpatialNode() : this(new Vector3(0, 0, 0)) { }

		public SpatialNode( Vector3 position ) : base()
		{
			Position = position;
		}

		public SpatialNode(Vector3 position, float size, Color color) : base( size, color )
		{
			Position = position;
		}


		public override string GetInfo()
		{
			return ( base.GetInfo() + ",X:" + Position.X + ",Y:" + Position.Y + ",Z:" + Position.Z );
		}

	}
}
