using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GraphVis
{
	public class Graph
	{
		public struct Edge
		{
			public int		End1;
			public int		End2;
			public float	Length;
			public float	Value;

			public Edge(int end1, int end2, float length, float value)
			{
				End1 = end1;
				End2 = end2;
				Length = length;
				Value = value;
			}

			public string GetInfo()
			{
				return "id1:" + End1 + ",id2:" + End2 + ",length:" + Length + ",value:" + Value;
			}
		}

		List<BaseNode>	nodeList;
		List<Edge>	edgeList;
		List<List<int>> adjacencyList;

		public int NodeCount { get { return nodeList.Count; } }
		public int EdgeCount { get { return edgeList.Count; } }

		public List<BaseNode>	Nodes { get { return nodeList; } }
		public List<Edge>		Edges { get { return edgeList; } }
		public List<List<int>> AdjacencyList { get { return adjacencyList; } }


		public Graph()
		{
			nodeList = new List<BaseNode>();
			edgeList	= new List<Edge>();
			adjacencyList = new List<List<int>>();
		}

		public void AddNode(BaseNode node)
		{
			node.Id = nodeList.Count;
			nodeList.Add( node );
			adjacencyList.Add( new List<int>() );
		}

		public void AddEdge(int node1, int node2)
		{
			edgeList.Add( new Edge
				{
					End1 = node1,
					End2 = node2,
					Length = 1.0f,
					Value = 0.1f,
				}
			);
			adjacencyList[node1].Add( EdgeCount - 1 );
			adjacencyList[node2].Add( EdgeCount - 1 );
		}



		public void AddEdge(Edge edge)
		{
			edgeList.Add(edge);
			adjacencyList[edge.End1].Add(EdgeCount - 1);
			adjacencyList[edge.End2].Add(EdgeCount - 1);
		}


		public void CollapseEdge(int edgeIndex)
		{
			var edge = edgeList[edgeIndex];
			MergeNodes(edge.End1, edge.End2);
		}


        public void MergeNodes(int node1, int node2)
		{
			int detachedNode = node1 > node2 ? node1 : node2;
			int remainNode = node1 < node2 ? node1 : node2;
			var adjEdges = GetEdges(detachedNode);
			foreach (int adjEdge in adjEdges)
			{
				var edge = edgeList[adjEdge];
				if (edge.End1 == detachedNode)
				{
					edge.End1 = remainNode;
				}
				else
				{
					edge.End2 = remainNode;
				}
				edgeList[adjEdge] = edge;
			}
			AdjacencyList[detachedNode].Clear();
		}



		public List<int> GetEdges(int nodeIndex)
		{
			List<int> adjEdges = new List<int>();
			foreach (var adjEdge in AdjacencyList[nodeIndex])
			{
				adjEdges.Add(adjEdge);
			}
			return adjEdges;
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
				AddNode(new BaseNode());
				AddEdge( index, newNodeIndex );
			}
			return true;
		}



		public static Graph MakeHub(int childrenCount)
		{
			Graph graph = new Graph();
			graph.AddNode(new BaseNode());
			graph.AddChildren( childrenCount, 0 );
			return graph;
		}

		public static Graph MakeString(int nodeCount)
		{
			Graph graph = new Graph();

			graph.AddNode(new BaseNode());
			for (int i = 1; i < nodeCount; ++i)
			{
				graph.AddNode(new BaseNode());
				graph.AddEdge(graph.NodeCount - 2, graph.NodeCount - 1);
			}
			return graph;
		}


		public static Graph MakeRing(int nodeCount)
		{
			var graph = MakeString( nodeCount );
			graph.AddEdge(graph.NodeCount - 1, 0);
			return graph;
		}


		public static Graph MakeTree(int nodeCount, int arity)
		{
			Graph graph = new Graph();

			graph.AddNode(new BaseNode());
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


		
		public static Graph MakeBinaryTree(int nodeCount)
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
			float	[] distances	= new float	[NodeCount];
			int		[] previous		= new int	[NodeCount];
			Dictionary<int, float> estim = new Dictionary<int,float>();

			for (int i = 0; i < NodeCount; ++i)
			{
				estim.Add(i, 9999999);
				previous[i] = i;
			}
			estim[src] = 0;
			while (estim.Count > 0)
			{
				var cheapestNode  = estim.OrderBy( pair => pair.Value ).First();
				estim.Remove(cheapestNode.Key);
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

		public virtual void WriteToFile(string path)
		{
			using (StreamWriter wr = new StreamWriter(path))
			{
				wr.WriteLine("nodes:");
				foreach (var node in Nodes)
				{
					wr.WriteLine(node.GetInfo());
				}
				wr.WriteLine("edges:");
				foreach (var edge in Edges)
				{
					wr.WriteLine(edge.GetInfo());
				}
			}
		}


	}
}
