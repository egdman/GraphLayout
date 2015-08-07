using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GraphVis
{
	public class CitationGraph : GraphFromFile
	{
		public override void ReadFromFile(string path)
		{
			for (int i = 0; i < 1000; ++i)
			{
				AddNode(new BaseNode());
			}
			var dstrings = File.ReadAllLines(path);
			if (dstrings.Length > 0)
			{
				for (int i = 0; i < dstrings.Length; i = i + 2)
				{
					string citName = dstrings[i];
					string[] citations;
					citations = dstrings[i + 1].Split(new Char[] { '\t', ' ', ',' });

					foreach (string cit in citations)
					{
						if (cit != "")
						{
							AddEdge(int.Parse(citName), int.Parse(cit));
						}
					}
				}
			}
		}
	}
}
