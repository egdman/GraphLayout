using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Fusion;
using Fusion.Graphics;
using Fusion.Mathematics;
using Fusion.Input;

namespace GraphVis
{
	// particle in 3d space:
	[StructLayout(LayoutKind.Explicit)]
	public struct Particle3d
	{
		[FieldOffset(0)]
		public Vector3 Position;
		[FieldOffset(12)]
		public Vector3 Velocity;
		[FieldOffset(24)]
		public Vector3 Force;
		[FieldOffset(36)]
		public float Energy;
		[FieldOffset(40)]
		public float Mass;
		[FieldOffset(44)]
		public float Charge;

		[FieldOffset(48)]
		public Vector4 Color;
		[FieldOffset(64)]
		public float Size;
		[FieldOffset(68)]
		public int linksPtr;
		[FieldOffset(72)]
		public int linksCount;
	}


	// link between 2 particles:
	[StructLayout(LayoutKind.Explicit)]
	public struct Link
	{
		[FieldOffset(0)]
		public uint par1;
		[FieldOffset(4)]
		public uint par2;
		[FieldOffset(8)]
		public float length;
	}


	[StructLayout(LayoutKind.Explicit)]
	public struct LinkId
	{
		[FieldOffset(0)]
		public int id;
	}


	class Calculator : IDisposable
	{
		
		[StructLayout(LayoutKind.Explicit, Size = 16)]
		struct ComputeParams
		{
			[FieldOffset(0)]
			public uint MaxParticles;
			[FieldOffset(4)]
			public float StepLength;
			[FieldOffset(8)]
			public float LinkSize;
		}


		enum ComputeFlags
		{
			COMPUTE			= 0x1,
			INJECTION		= 0x1 << 1,
			SIMULATION		= 0x1 << 2,
			MOVE			= 0x1 << 3,
			REDUCTION		= 0x1 << 4,
			EULER			= 0x1 << 5,
			RUNGE_KUTTA		= 0x1 << 6,
			LINKS			= 0x1 << 7
		}

		public enum State
		{
			RUN,
			PAUSE
		}

		// size of a thread group:
		const int BlockSize = 256;

		public StructuredBuffer CurrentStateBuffer
		{
			get;
			set;
		}

		public StructuredBuffer NextStateBuffer
		{
			get;
			set;
		}

		public StructuredBuffer LinksBuffer
		{
			get;
			set;
		}

		public StructuredBuffer LinksIndexBuffer
		{
			get;
			set;
		}

		public StructuredBuffer EnergyBuffer
		{
			get;
			set;
		}

		Ubershader			shader;
		StateFactory		factory;
		ConstantBuffer		paramsCB;

		float	linkSize;

		float	stepLength;
		float	energy;
		float	deltaEnergy;
		float	pGradE;
		int		numIterations;

		int		stepStability;
		float	checkSum;
		int		numParticles;


		public int ParticleCount
		{
			get { return numParticles; }
		}

		public int NumberOfIterations
		{
			get { return numIterations; }
		}

		public State RunPause
		{
			get;
			set;
		}


		public bool DisableAutoStep
		{
			get;
			set;
		}

		Game	env;



		// Constructor: ----------------------------------------------------------------------------------------
		public Calculator(Game game)
		{
			env = game;
			shader = env.Content.Load<Ubershader>("Compute");
			factory = new StateFactory(shader, typeof(ComputeFlags), (plState, comb) =>
			{
				plState.RasterizerState = RasterizerState.CullNone;
				plState.BlendState = BlendState.NegMultiply;
				plState.DepthStencilState = DepthStencilState.Readonly;
				plState.Primitive = Primitive.PointList;
			});

			paramsCB = new ConstantBuffer(env.GraphicsDevice, typeof(ComputeParams));

			linkSize			=	100.0f;			

			stepLength			=	1.0f;
			numIterations		=	0;
			FixedStep			=	false;

			stepStability		=	0;
			checkSum			=	0;
		}
		// ----------------------------------------------------------------------------------------------------


		public bool FixedStep
		{
			get;
			set;
		}


		public bool UseGPU
		{
			get;
			set;
		}


		public void Pause()
		{
			if (RunPause == State.RUN) RunPause = State.PAUSE;
			else RunPause = State.RUN;
		}


		void disposeOfBuffers()
		{
			if (CurrentStateBuffer != null)
			{
				CurrentStateBuffer.Dispose();
			}

			if (NextStateBuffer != null)
			{
				NextStateBuffer.Dispose();
			}

			if (LinksBuffer != null)
			{
				LinksBuffer.Dispose();
			}

			if (LinksIndexBuffer != null)
			{
				LinksIndexBuffer.Dispose();
			}

			if (EnergyBuffer != null)
			{
				EnergyBuffer.Dispose();
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				paramsCB.Dispose();
				disposeOfBuffers();
				if (factory != null)
				{
					factory.Dispose();
				}
				if (shader != null)
				{
					shader.Dispose();
				}
			}
		}



		public void SetData(List<Particle3d> ParticleList, List<Link> linkList, List<List<int>> linkIndexLists)
		{
			numParticles = ParticleList.Count;

			Particle3d[] particleBufferCPU = ParticleList.ToArray();

			Link[] linksBufferCPU = linkList.ToArray();
			LinkId[] linksPtrBufferCPU = new LinkId[linkList.Count * 2];
			int iter = 0;
			int lpIter = 0;
			foreach (var ptrList in linkIndexLists)
			{

				int blockSize = 0;
				particleBufferCPU[iter].linksPtr = lpIter;
				if (ptrList != null)
				{
					foreach (var linkPtr in ptrList)
					{
						linksPtrBufferCPU[lpIter] = new LinkId { id = linkPtr };
						++lpIter;
						++blockSize;
					}
				}
				particleBufferCPU[iter].linksCount = blockSize;
				++iter;
			}

			disposeOfBuffers();

			if (particleBufferCPU.Length != 0)
			{
				CurrentStateBuffer = new StructuredBuffer(env.GraphicsDevice, typeof(Particle3d), particleBufferCPU.Length, StructuredBufferFlags.Counter);
				NextStateBuffer = new StructuredBuffer(env.GraphicsDevice, typeof(Particle3d), particleBufferCPU.Length, StructuredBufferFlags.Counter);
				CurrentStateBuffer.SetData(particleBufferCPU);
				EnergyBuffer = new StructuredBuffer(
							env.GraphicsDevice,
							typeof(Vector4),
							MathUtil.IntDivUp(particleBufferCPU.Length, BlockSize),
							StructuredBufferFlags.Counter);
			}
			if (linksBufferCPU.Length != 0)
			{
				LinksBuffer = new StructuredBuffer(
							env.GraphicsDevice,
							typeof(Link),
							linksBufferCPU.Length,
							StructuredBufferFlags.Counter);
				LinksBuffer.SetData(linksBufferCPU);
			}
			if (linksPtrBufferCPU.Length != 0)
			{
				LinksIndexBuffer = new StructuredBuffer(
							env.GraphicsDevice,
							typeof(LinkId),
							linksPtrBufferCPU.Length,
							StructuredBufferFlags.Counter);
				LinksIndexBuffer.SetData(linksPtrBufferCPU);
			}
			initCalculations();
		}



		void SwapParticleBuffers()
		{
			var temp = NextStateBuffer;
			NextStateBuffer = CurrentStateBuffer;
			CurrentStateBuffer = temp;
		}


		public void ResetState()
		{
			stepLength = 0.1f;
			numIterations = 0;
			stepStability = 0;
		}


		public void Update(int userCommand)
		{
			var device = env.GraphicsDevice;
			var graphSys = env.GetService<GraphSystem>();
			bool cond1 = false;
			bool cond2 = false;

			float energyThreshold = (float)ParticleCount / 10000.0f;
			float chosenStepLength = stepLength;

			if (DisableAutoStep)
			{
				FixedStep = true;
			}

			// Wolfe constants:
			//		float C1 = 0.1f;
			//		float C2 = 0.99f;

			float C1 = 0.3f;
			float C2 = 0.99f;

			//			stepLength = 0.1f;

			// Algorithm outline:
			//
			//  1. Calc descent vector pk
			//  2. calc pk * grad(Ek)
			//  3. calc Ek
			//  4. try move with some step factor
			//  5. calc pk * grad(Ek+1)
			//  6. calc Ek+1
			//  7. check Wolfe conditions
			//  8. if both are OK GOTO 11
			//  9. modify step factor
			// 10. GOTO 4
			// 11. swap try buffer with current buffer
			// 12. GOTO 1
			// 

			ComputeParams param = new ComputeParams();
			param.MaxParticles = 0;
			param.LinkSize = linkSize;


			if (CurrentStateBuffer != null)
			{

				param.MaxParticles = (uint)ParticleCount;

				// manual step change:
				if (userCommand > 0)
				{
					stepLength = increaseStep(stepLength);
				}
				if (userCommand < 0)
				{
					stepLength = decreaseStep(stepLength);
				}


				if (RunPause == State.RUN)
				{

					//			StreamWriter sw = File.AppendText( "step.csv" );

					for (int i = 0; i < graphSys.Config.IterationsPerFrame; ++i)
					{
						float Ek = energy;
						float Ek1 = 0;

						float pkGradEk = 0;
						float pkGradEk1 = 0;

						cond1 = false;
						cond2 = false;

						int tries = 0;

						if (!FixedStep)
						{
							calcTotalEnergyAndDotProduct(device, CurrentStateBuffer, CurrentStateBuffer,
									EnergyBuffer, param, out Ek, out pkGradEk, out checkSum);
						}

						while (!(cond1 && cond2))
						{
							if (FixedStep)
							{
								cond1 = true;
								cond2 = true;
							}

							param.StepLength = stepLength;

							moveVertices(device, CurrentStateBuffer, NextStateBuffer, param);
							calcDescentVector(device, NextStateBuffer, param); // calc energies

							if (!FixedStep)
							{
								calcTotalEnergyAndDotProduct(device, CurrentStateBuffer, NextStateBuffer,
										EnergyBuffer, param, out Ek1, out pkGradEk1, out checkSum);


								// check Wolfe conditions:
								cond1 = (Ek1 - Ek <= stepLength * C1 * pkGradEk);
								cond2 = (pkGradEk1 >= C2 * pkGradEk);

								// if we are very close to minimum, do not check conditions (it leads to infinite cycles)
								if (Math.Abs(Ek1 - Ek) < energyThreshold)
								{
									cond1 = cond2 = true;
								}

								// Debug output:
								if (tries > 4)
								{
									Console.WriteLine("step = " + stepLength + " " +
										"cond#1 = " + (cond1 ? "TRUE" : "FALSE") + " " +
										"cond#2 = " + (cond2 ? "TRUE" : "FALSE") + " " +
										"deltaE = " + (Ek1 - Ek)
										);
								}

								// change step length:
								if (cond1 && !cond2) { stepLength = increaseStep(stepLength); }
								if (!cond1 && !cond2) { stepLength = increaseStep(stepLength); }

								if (!cond1 && cond2) { stepLength = decreaseStep(stepLength); }

							}
							++tries;
							++numIterations;

							// To prevent freeze:
							if (tries > graphSys.Config.SearchIterations) break;
						}
						// swap buffers: --------------------------------------------------------------------
						var temp = CurrentStateBuffer;
						CurrentStateBuffer = NextStateBuffer;
						NextStateBuffer = temp;

						if (stepLength == chosenStepLength) // if the new step length is the same as before
						{
							++stepStability;
						}
						else
						{
							stepStability = 0;
						}
						chosenStepLength = stepLength;

						if (stepStability >= graphSys.Config.SwitchToManualAfter) // if stable step length found, switch to manual
						{
							FixedStep = true;
						}

						energy = Ek1;
						deltaEnergy = Ek1 - Ek;
						pGradE = pkGradEk1;
						
					}
				}
			}

			var debStr = env.GetService<DebugStrings>();

			debStr.Add(Color.Aqua, "Step factor  = " + chosenStepLength);
	//		debStr.Add(Color.Aqua, "Energy           = " + energy);
	//		debStr.Add(Color.Aqua, "DeltaEnergy      = " + deltaEnergy);
	//		debStr.Add(Color.Aqua, "pTp              = " + pGradE);
			debStr.Add(Color.Aqua, "Iteration        = " + numIterations);
	//		debStr.Add(Color.Aqua, "Stability         = " + stepStability);
	//		debStr.Add(Color.Orchid, "Check sum       = " + checkSum);
		}


		/// <summary>
		/// This function returns increased step length
		/// </summary>
		/// <param name="step"></param>
		/// <returns></returns>
		float increaseStep(float step)
		{
			return step + 0.01f;
		}


		/// <summary>
		/// This function returns decreased step length
		/// </summary>
		/// <param name="step"></param>
		/// <returns></returns>
		float decreaseStep(float step)
		{
			return step - 0.01f;
		}


		/// <summary>
		/// This function performs the first iteration of calculations
		/// </summary>
		void initCalculations()
		{
			ComputeParams param = new ComputeParams();
			param.MaxParticles = (uint)ParticleCount;

			var device = env.GraphicsDevice;
			calcDescentVector(device, CurrentStateBuffer, param); // calc desc vector and energies
			calcTotalEnergyAndDotProduct(device, CurrentStateBuffer, CurrentStateBuffer,
					EnergyBuffer, param, out energy, out pGradE, out checkSum);
		}


		/// <summary>
		/// This function calculates two values:
		/// 1. Energy E for every vertex				(N floats)
		/// 2. Descent vector which equals -grad(E)		(3*N floats)
		/// Values are overwritten into the same buffer
		/// </summary>
		/// <param name="device"></param>
		/// <param name="rwVertexBuffer"></param>
		/// <param name="parameters"></param>
		void calcDescentVector(GraphicsDevice device, StructuredBuffer rwVertexBuffer, ComputeParams parameters)
		{
			paramsCB.SetData(parameters);
			device.ComputeShaderConstants[0] = paramsCB;
			device.SetCSRWBuffer(0, rwVertexBuffer, (int)parameters.MaxParticles);
			device.ComputeShaderResources[2] = LinksIndexBuffer;
			device.ComputeShaderResources[3] = LinksBuffer;
			device.PipelineState = factory[(int)(ComputeFlags.COMPUTE | ComputeFlags.SIMULATION | ComputeFlags.EULER | ComputeFlags.LINKS)];
			device.Dispatch(MathUtil.IntDivUp((int)parameters.MaxParticles, BlockSize));
			device.ResetStates();
		}



		/// <summary>
		/// This function takes vertices from the source buffer and moves them by
		/// descent vector times stepLength (Pk*stepLength).
		/// Then it writes them into the destination buffer with new positions.
		/// This function does not change data in the source buffer.
		/// </summary>
		/// <param name="device"></param>
		/// <param name="srcVertexBuffer"></param>
		/// <param name="dstVertexBuffer"></param>
		/// <param name="parameters"></param>
		void moveVertices(GraphicsDevice device, StructuredBuffer srcVertexBuffer,
							StructuredBuffer dstVertexBuffer, ComputeParams parameters)
		{
			paramsCB.SetData(parameters);
			device.ComputeShaderConstants[0] = paramsCB;
			device.ComputeShaderResources[0] = srcVertexBuffer;
			device.SetCSRWBuffer(0, dstVertexBuffer, (int)parameters.MaxParticles);
			device.PipelineState = factory[(int)(ComputeFlags.COMPUTE | ComputeFlags.MOVE)];
			device.Dispatch(MathUtil.IntDivUp((int)parameters.MaxParticles, BlockSize));
			device.ResetStates();
		}



		/// <summary>
		/// This function calculates the following values:
		/// 1. Total energy at (k+1)th iteration (from nextStateBuffer).
		/// 2. Dot product of kth descent vector and (k+1)th energy gradient (which equals minus (k+1)th descent vector)
		/// </summary>
		/// <param name="device"></param>
		/// <param name="currentStateBuffer"></param>
		/// <param name="nextStateBuffer"></param>
		/// <param name="outputValues"></param>
		/// <param name="parameters"></param>
		/// <param name="energy"></param>
		/// <param name="pTgradE"></param>
		void calcTotalEnergyAndDotProduct(GraphicsDevice device, StructuredBuffer currentStateBuffer,
			StructuredBuffer nextStateBuffer, StructuredBuffer outputValues, ComputeParams parameters,
			out float energy, out float pTgradE, out float checkSum)
		{
			energy = 0;
			pTgradE = 0;
			checkSum = 0;

			if (UseGPU)
			{
				// preform reduction on GPU:
				paramsCB.SetData(parameters);
				device.ComputeShaderConstants[0] = paramsCB;
				device.ComputeShaderResources[0] = currentStateBuffer;
				device.ComputeShaderResources[1] = nextStateBuffer;
				device.SetCSRWBuffer(1, outputValues, MathUtil.IntDivUp((int)parameters.MaxParticles, BlockSize));
				device.PipelineState = factory[(int)ComputeFlags.COMPUTE | (int)ComputeFlags.REDUCTION];
				device.Dispatch(MathUtil.IntDivUp((int)parameters.MaxParticles, BlockSize));

				// perform final summation:
				Vector4[] valueBufferCPU = new Vector4[MathUtil.IntDivUp((int)parameters.MaxParticles, BlockSize)];
				outputValues.GetData(valueBufferCPU);
				foreach (var value in valueBufferCPU)
				{
					energy += value.X;
					pTgradE += value.Y;
					checkSum += value.Z;
				}
			}
			else // if not use GPU
			{
				// perform summation on CPU:
				Particle3d[] currentBufferCPU = new Particle3d[parameters.MaxParticles];
				Particle3d[] nextBufferCPU = new Particle3d[parameters.MaxParticles];

				currentStateBuffer.GetData(currentBufferCPU);
				nextStateBuffer.GetData(nextBufferCPU);


				for (int i = 0; i < parameters.MaxParticles; ++i)
				{
					Vector3 force1 = currentBufferCPU[i].Force;
					Vector3 force2 = nextBufferCPU[i].Force;

					pTgradE += -1.0f * Vector3.Dot(force1, force2);
					energy += nextBufferCPU[i].Energy;
					checkSum += nextBufferCPU[i].Mass;
				}
			}

			energy /= 2; // because each pair is counted 2 times
		}


	}
}
