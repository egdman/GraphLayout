using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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
			node.Id = nodeList.Count;
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



		public static Graph<Node> MakeHub(int childrenCount)
		{
			Graph<Node> graph = new Graph<Node>();
			graph.AddNode ( new Node() );
			graph.AddChildren( childrenCount, 0 );
			return graph;
		}

		public static Graph<Node> MakeString(int nodeCount)
		{
			Graph<Node> graph = new Graph<Node>();

			graph.AddNode(new Node());
			for (int i = 1; i < nodeCount; ++i)
			{
				graph.AddNode(new Node());
				graph.AddEdge(graph.NodeCount - 2, graph.NodeCount - 1);
			}
			return graph;
		}


		public static Graph<Node> MakeRing(int nodeCount)
		{
			var graph = MakeString( nodeCount );
			graph.AddEdge(graph.NodeCount - 1, 0);
			return graph;
		}


		public static Graph<Node> MakeTree(int nodeCount, int arity)
		{
			Graph<Node> graph = new Graph<Node>();

			graph.AddNode(new Node());
			graph.AddChildren(arity, graph.NodeCount - 1);

			Queue<int> latestIndex = new Queue<int>();
			for (int i = 0; i < arity; ++i)
			{
				latestIndex.Enqueue(graph.NodeCount - 1 - i);
			}

			while (graph.NodeCount < nodeCount)
			{
				if (latestIndex.Count <= 0)
				{
					break;
				}
				int parentIndex = latestIndex.Peek();

				if (graph.adjacencyList[parentIndex].Count > arity)
				{
					latestIndex.Dequeue();
					continue;
				}
				graph.AddChildren(1, parentIndex);
				latestIndex.Enqueue(graph.NodeCount - 1);
			}
			return graph;
		}


		
		public static Graph<Node> MakeBinaryTree(int nodeCount)
		{
			return MakeTree( nodeCount, 2 );
		}


		public float GetCentrality( int index )
		{
			float centrality = 0;
			float [] paths = shortestPaths(index);
			for (int index2 = 0; index2 < NodeCount; ++index2)
			{
				if ( index2 != index ) centrality += ( 1 / paths[index2] );
			}
			return centrality;
		}



		float [] shortestPaths(int src)
		{
		//	int		nodesRemain		= NodeCount;
			float	[] distances	= new float	[NodeCount];
			int		[] previous		= new int	[NodeCount];
		//	bool	[] done			= new bool	[NodeCount];
			Dictionary<int, float> estim = new Dictionary<int,float>();

			for (int i = 0; i < NodeCount; ++i)
			{
				estim.Add(i, 9999999);
				previous[i] = i;
			}
			estim[src] = 0;
			while (estim.Count > 0)
		//	while(nodesRemain > 0)
			{
				var cheapestNode  = estim.OrderBy( pair => pair.Value ).First();
				estim.Remove(cheapestNode.Key);
		//		--nodesRemain;
				distances[cheapestNode.Key] = cheapestNode.Value;
				foreach (var edge in adjacencyList[cheapestNode.Key])
				{
					relax( cheapestNode.Key, cheapestNode.Value, edge, estim, previous );
				}
			}
			return distances;
		}


		void relax( int src, float srcEstim, int edge, Dictionary<int, float> estim, int [] previous )
		{
			int neighbor = Edges[edge].End1 == src ? Edges[edge].End2 : Edges[edge].End1;

			float d = Edges[edge].Length;
			float neighborEstim;
			if (!estim.TryGetValue( neighbor, out neighborEstim ) ) return;
			if (neighborEstim > srcEstim + d)
			{
				estim[neighbor] = srcEstim + d;
				previous[neighbor] = src;
			}
		}

		float dist(int index1, int index2)
		{
			foreach (int edge in adjacencyList[index1])
			{
				if (Edges[edge].End1 == index2 || Edges[edge].End2 == index2)
				{
					return Edges[edge].Length;
				}
			}
			throw new InvalidOperationException( "Requested edges are not adjacent" );
		}

		public void WriteToFile(string path)
		{
			using (StreamWriter wr = new StreamWriter(path))
			{
				foreach (var node in Nodes)
				{
					wr.WriteLine(node.GetInfo());
				}
			}
		}


	}
}
