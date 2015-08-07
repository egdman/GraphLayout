using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphVis
{
	public class SpatialGraph : Graph
	{

		public SpatialGraph() : base() { }

		public void AddNode(SpatialNode node)
		{
			base.AddNode(node);
		}
	}
}
