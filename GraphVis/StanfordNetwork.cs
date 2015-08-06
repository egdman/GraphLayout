using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GraphVis
{
	public class StanfordNetwork<Node> : GraphFromFile<Node> where Node : INode, new()
	{

		public override void ReadFromFile(string path)
		{
			
			var lines = File.ReadAllLines(path);
			Dictionary<int, int> nodeId_NodeNumber = new Dictionary<int, int>();

			int numOfNodesAdded = 0;
			if (lines.Length > 0)
			{
				// construct dictionary to convert id to number:
				foreach (var line in lines)
				{
					if (line.ElementAt(0) != '#')
					{
						string[] parts;
						parts = line.Split(new Char[] { '\t', ' ' });
						int index1 = int.Parse(parts[0]);
						int index2 = int.Parse(parts[1]);

						if (!nodeId_NodeNumber.ContainsKey(index1))
						{
							nodeId_NodeNumber.Add(index1, numOfNodesAdded);
							++numOfNodesAdded;
						}
						if (!nodeId_NodeNumber.ContainsKey(index2))
						{
							nodeId_NodeNumber.Add(index2, numOfNodesAdded);
							++numOfNodesAdded;
						}
					}
				}


				// add nodes:
				for (int i = 0; i < nodeId_NodeNumber.Count; ++i)
				{
					AddNode(new Node());
				}

				// add edges:
				foreach ( var line in lines )
				{
					if (line.ElementAt(0) != '#')
					{
						string[] parts;
						parts = line.Split(new Char[] {'\t', ' '});
						int index1 = int.Parse(parts[0]);
						int index2 = int.Parse(parts[1]);
						AddEdge(nodeId_NodeNumber[index1], nodeId_NodeNumber[index2]);
					}
				}
			}
		}
	}
}
