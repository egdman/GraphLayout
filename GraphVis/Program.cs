using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Development;
using Fusion.Mathematics;
using System.Diagnostics;

namespace GraphVis
{
	class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			Trace.Listeners.Add(new ColoredTraceListener());
			using (var game = new GraphVis())
			{
				if (DevCon.Prepare(game, @"..\..\..\Content\Content.xml", "Content"))
				{
					game.Run(args);
				}
			}
		}
	}
}
