using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphVis
{
	class Program
	{

		[STAThread]
		static void Main(string[] args)
		{
	//		using (var game = new GraphVis())
	//		{
	//			game.Parameters.TrackObjects = true;
	//			game.Run(args);
				
	//		}
			GraphVis.Run( new GraphVis(), true, @"..\..\..\Content" );
		}
	}
}
