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
	public interface INode
	{
		float GetSize();
		Color GetColor();
		string GetInfo();
		int	Id{ get; set; }
		
	}
}
