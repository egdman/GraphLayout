using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Fusion;
using Fusion.Graphics;
using Fusion.Mathematics;
using Fusion.Input;

namespace GraphVis
{
	class ProteinGraph : GraphFromFile
	{

		class Interaction
		{
			public int id1;
			public int id2;
			public string type;
		}

		public override void ReadFromFile(string path)
		{
			var lines = File.ReadAllLines(path);

			Dictionary<int, int> uniqueIds = new Dictionary<int, int>();

			List<Interaction> interactions = new List<Interaction>();
			int numNodes = 0;
			foreach (var line in lines)
			{
				string [] parts = line.Split(new Char[] { '\t', ',' });
				string name = parts[0];
				int	id		= int.Parse(parts[1]);
				int	otherId	= -1;
				if (parts[3].Length > 0)
				{
					otherId = int.Parse(parts[2]);
				}
				string edgeType = parts[3];

				int cat1 = int.Parse(parts[4]);
				int cat2 = int.Parse(parts[5]);
				int cat3 = int.Parse(parts[6]);

				if (!uniqueIds.ContainsKey(id))
				{
					uniqueIds.Add(id, numNodes);
					++numNodes;

					Color color = new Color((float)cat1, (float)cat2, (float)cat3);
					AddNode(new NodeWithText(name, 1.0f, color));
				}
				if (otherId >= 0)
				{
					interactions.Add(new Interaction{id1 = id, id2 = otherId, type = edgeType});
				}
			}

			foreach (var inter in interactions)
			{
				addInteraction(uniqueIds[inter.id1], uniqueIds[inter.id2], inter.type);
			}
		}

		void addInteraction(int id1, int id2, string interType)
		{
			Edge edge = new Edge();
			edge.End1 = id1;
			edge.End2 = id2;
			edge.Length = 1.0f;

			float strength = 0;
			if (interType.Length == 0)
			{
				return;
			}
			if (interType[0] == '+')
			{
				strength = 0.5f;
			}
			else if (interType[0] == '-')
			{
				strength = 0.5f;
			}
			else if (interType[0] == 'b')
			{
				strength = 5.0f;
			}
			else
			{
				strength = 0;
			}
			edge.Value = strength;
			AddEdge(edge);
		}
	}
}
