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
	class NodeWithText : BaseNode
	{
		string text;

		public NodeWithText(string Text)
		{
			text = Text;
		}


		public NodeWithText(string Text, float size, Color color)
			: base(size, color)
		{
			text = Text;
		}


		public NodeWithText() : this("no text") { }

		public override string GetInfo()
		{
			return (base.GetInfo() + ",text:" + text);
		}

		public string Text { get { return text; } set { text = value; } }

	}
}
