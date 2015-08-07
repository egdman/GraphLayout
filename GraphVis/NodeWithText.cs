using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphVis
{
	class NodeWithText : BaseNode
	{
		string text;

		public NodeWithText(string Text)
		{
			text = Text;
		}

		public NodeWithText() : this("no text") { }

		public override string GetInfo()
		{
			return (base.GetInfo() + ", " + text);
		}

		public string Text { get { return text; } set { text = value; } }

	}
}
