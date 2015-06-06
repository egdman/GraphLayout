using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphVis
{
	public class Graph<Node> where Node : INode, new()
	{
		public struct Edge
		{
			public int		End1;
			public int		End2;
			public float	Length;
		}

		List<Node>	nodeList;
		List<Edge>	edgeList;
		List<List<int>> adjacencyList;

		public int NodeCount { get { return nodeList.Count; } }
		public int EdgeCount { get { return edgeList.Count; } }

		public List<Node> Nodes { get { return nodeList; } }
		public List<Edge> Edges { get { return edgeList; } }
		public List<List<int>> AdjacencyList { get { return adjacencyList; } }


		public Graph()
		{
			nodeList	= new List<Node>();
			edgeList	= new List<Edge>();
			adjacencyList = new List<List<int>>();
		}


		public void AddNode(Node node)
		{
			nodeList.Add( node );
			adjacencyList.Add( new List<int>() );
		}

		public void AddEdge(int index1, int index2)
		{
			edgeList.Add( new Edge
				{
					End1 = index1,
					End2 = index2,
					Length = 1.0f
				}
			);
			adjacencyList[index1].Add( EdgeCount - 1 );
			adjacencyList[index2].Add( EdgeCount - 1 );
		}

		public bool AddChildren(int number, int index)
		{
			if (number <= 0)
			{
				return false;
			}
			if (index >= NodeCount)
			{
				return false;
			}
			for (int i = 0; i < number; ++i)
			{
				int newNodeIndex = NodeCount;
				AddNode( new Node() );
				AddEdge( index, newNodeIndex );
			}
			return true;
		}



		public static Graph<Node> MakeBinaryTree(int nodeCount)
		{
			Graph<Node> graph = new Graph<Node>();

			graph.AddNode( new Node() );
			graph.AddChildren( 2, graph.NodeCount - 1 );

			Queue<int> latestIndex = new Queue<int>();
			latestIndex.Enqueue(graph.NodeCount - 1);
			latestIndex.Enqueue(graph.NodeCount - 2);

			while (graph.NodeCount < nodeCount)
			{
				if (latestIndex.Count <= 0)
				{
					break;
				}
				int parentIndex = latestIndex.Peek();

				if (graph.adjacencyList[parentIndex].Count > 2)
				{
					latestIndex.Dequeue();
					continue;
				}
				graph.AddChildren( 1, parentIndex );
				latestIndex.Enqueue(graph.NodeCount - 1);
			}
			return graph;
		}

	}
}
