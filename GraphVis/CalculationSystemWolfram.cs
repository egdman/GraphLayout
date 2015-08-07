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
	class CalculationSystemWolfram : CalculationSystem
	{
		float stepLength;
		float energy;
		float deltaEnergy;
		float checkSum;

		int progress;
		const int maxProgress = 5;

		public CalculationSystemWolfram(LayoutSystem host)
			: base(host)
		{
			stepLength		= 1.0f;
			checkSum = 0;
			energy = 0;
			deltaEnergy = 0;
			progress = 0;
		}


		protected override void resetState()
		{
			stepLength = HostSystem.Environment.GetService<GraphSystem>().Config.StepSize;
			numIterations = 0;
			energy = 0;
			deltaEnergy = 0;
			progress = 0;
		}


		protected override void initCalculations()
		{
			LayoutSystem.ComputeParams param = new LayoutSystem.ComputeParams();
			float useless = 0;
			param.StepLength = stepLength;
			HostSystem.CalcDescentVector(HostSystem.CurrentStateBuffer, param); // calc desc vector and energies
			HostSystem.CalcTotalEnergyAndDotProduct(HostSystem.CurrentStateBuffer, HostSystem.CurrentStateBuffer,
					HostSystem.EnergyBuffer, param, out energy, out useless, out checkSum);
		}


		protected override void update(int userCommand)
		{
			var graphSys = HostSystem.Environment.GetService<GraphSystem>();
			LayoutSystem.ComputeParams param = new LayoutSystem.ComputeParams();
			param.StepLength = stepLength;
			float nextEnergy = 0;
			float useless = 0;
			if (HostSystem.CurrentStateBuffer != null)
			{
				if (HostSystem.RunPause == LayoutSystem.State.RUN)
				{
					for (int i = 0; i < graphSys.Config.IterationsPerFrame; ++i)
					{
						HostSystem.MoveVertices(
							HostSystem.CurrentStateBuffer,
							HostSystem.NextStateBuffer,
							param
						);

						HostSystem.CalcDescentVector(
							HostSystem.NextStateBuffer,
							param
						);

						HostSystem.CalcTotalEnergyAndDotProduct(
							HostSystem.NextStateBuffer,
							HostSystem.NextStateBuffer,
							HostSystem.EnergyBuffer,
							param,
							out nextEnergy,
							out useless,
							out checkSum
						);

						HostSystem.SwapBuffers();

						if (nextEnergy < energy)
						{
							++progress;
						}
						else
						{
							progress = 0;
							stepLength = decreaseStep(stepLength);
						}

						if (progress > maxProgress)
						{
							progress = 0;
							stepLength = increaseStep(stepLength);
						}
						deltaEnergy = nextEnergy - energy;
						energy = nextEnergy;
						++numIterations;
					}
				}
			}

			var debStr = HostSystem.Environment.GetService<DebugStrings>();

			debStr.Add(Color.Black, "WOLFRAM MODE");
			debStr.Add(Color.Aqua, "Step factor  = " + stepLength);
			debStr.Add(Color.Aqua, "Energy       = " + energy);
			debStr.Add(Color.Aqua, "deltaEnergy  = " + deltaEnergy);
			debStr.Add(Color.Aqua, "Iteration      = " + numIterations);
		}
		

		/// <summary>
		/// This function returns increased step length
		/// </summary>
		/// <param name="step"></param>
		/// <returns></returns>
		float increaseStep(float step)
		{
			return step * 1.01f;
		}


		/// <summary>
		/// This function returns decreased step length
		/// </summary>
		/// <param name="step"></param>
		/// <returns></returns>
		float decreaseStep(float step)
		{
			return step / 1.01f;
		}

	}
}
