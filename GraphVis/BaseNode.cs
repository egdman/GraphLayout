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
	public class BaseNode : INode
	{
		float nodeSize;
		Color nodeColor;

		public BaseNode( float size, Color color )
		{
			nodeColor = color;
			nodeSize = size;
		}

		// default constructor:
		public BaseNode()
			: this(3.0f, Color.White)
		{ }

		public float GetSize()
		{
			return nodeSize;
		}

		public Color GetColor()
		{
			return nodeColor;
		}

		public virtual string GetInfo()
		{
			var color = this.GetColor();
			return ("id:" + this.Id + ",size:" + this.GetSize() +
				",A:" + color.A + ",R:" + color.R + ",G:" + color.G + ",B:" + color.B );
		}


		public int Id { get; set; }
	}
}
