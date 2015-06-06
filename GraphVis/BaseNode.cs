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
			: this(1.0f, Color.White)
		{ }

		float INode.GetSize()
		{
			return nodeSize;
		}

		Color INode.GetColor()
		{
			return nodeColor;
		}
	}
}
