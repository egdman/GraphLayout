﻿using System;
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


	public class LayoutSystem : IDisposable
	{
		public enum StepMethod
		{
			Fixed,
			Auto,
			Wolfram
		}

		public StepMethod StepMode { get { return Environment.GetService<GraphSystem>().Config.StepMode; } }

		[StructLayout(LayoutKind.Explicit, Size = 16)]
		public struct ComputeParams
		{
			[FieldOffset(0)]
			public uint MaxParticles;
			[FieldOffset(4)]
			public float StepLength;
			[FieldOffset(8)]
			public float LinkSize;
			[FieldOffset(12)]
			public float SpringTension;
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
		int		numParticles;


		public int ParticleCount
		{
			get { return numParticles; }
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


		public float SpringTension
		{
			get;
			set;
		}

		Game	env;
		GraphicsDevice device;

		CalculationSystemAuto calcAuto;
		CalculationSystemFixed calcFixed;
		CalculationSystemWolfram calcWolfram;


		public Game Environment { get { return env; } }

		// Constructor: ----------------------------------------------------------------------------------------
		public LayoutSystem(Game game, Ubershader ubershader)
		{
			env = game;
			shader = ubershader;
			device = env.GraphicsDevice;
			factory = new StateFactory(shader, typeof(ComputeFlags), (plState, comb) =>
			{
				plState.RasterizerState = RasterizerState.CullNone;
				plState.BlendState = BlendState.NegMultiply;
				plState.DepthStencilState = DepthStencilState.Readonly;
				plState.Primitive = Primitive.PointList;
			});

			paramsCB = new ConstantBuffer(env.GraphicsDevice, typeof(ComputeParams));

			linkSize			=	100.0f;

			SpringTension = env.GetService<GraphSystem>().Config.SpringTension;
			calcAuto	= new CalculationSystemAuto(this);
			calcFixed	= new CalculationSystemFixed(this);
			calcWolfram = new CalculationSystemWolfram(this);
		}
		// ----------------------------------------------------------------------------------------------------


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


		public void ResetState()
		{	
			calcAuto.Reset();
			calcFixed.Reset();
			calcWolfram.Reset();
		}

		void initializeCalc()
		{
			SpringTension = env.GetService<GraphSystem>().Config.SpringTension;
			switch (StepMode)
			{
				case StepMethod.Auto:
					calcAuto.Initialize();
					break;
				case StepMethod.Fixed:
					calcFixed.Initialize();
					break;
				case StepMethod.Wolfram:
					calcWolfram.Initialize();
					break;
				default:
					break;
			}
		}

		public void Update(int userCommand)
		{
			switch (StepMode)
			{
				case StepMethod.Auto:
					calcAuto.Update(userCommand);
					break;
				case StepMethod.Fixed:
					calcFixed.Update(userCommand);
					break;
				case StepMethod.Wolfram:
					calcWolfram.Update(userCommand);
					break;
				default:
					break;
			}
		}

		public void SwapBuffers()
		{
			var temp = CurrentStateBuffer;
			CurrentStateBuffer = NextStateBuffer;
			NextStateBuffer = temp;
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
			initializeCalc();
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
		public void CalcDescentVector(StructuredBuffer rwVertexBuffer, ComputeParams parameters)
		{
			parameters.MaxParticles = (uint)ParticleCount;
			parameters.LinkSize = linkSize;
			parameters.SpringTension = SpringTension;
			paramsCB.SetData(parameters);
			device.ComputeShaderConstants[0] = paramsCB;
			device.SetCSRWBuffer(0, rwVertexBuffer, (int)parameters.MaxParticles);
			device.ComputeShaderResources[2] = LinksIndexBuffer;
			device.ComputeShaderResources[3] = LinksBuffer;
			device.PipelineState = factory[(int)(
				ComputeFlags.COMPUTE | ComputeFlags.SIMULATION |
				ComputeFlags.EULER | ComputeFlags.LINKS)];
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
		public void MoveVertices(StructuredBuffer srcVertexBuffer,
							StructuredBuffer dstVertexBuffer, ComputeParams parameters)
		{
			parameters.MaxParticles = (uint)ParticleCount;
			parameters.LinkSize = linkSize;
			parameters.SpringTension = SpringTension;
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
		public void CalcTotalEnergyAndDotProduct(StructuredBuffer currentStateBuffer,
			StructuredBuffer nextStateBuffer, StructuredBuffer outputValues, ComputeParams parameters,
			out float energy, out float pTgradE, out float checkSum)
		{
			parameters.MaxParticles = (uint)ParticleCount;
			parameters.LinkSize = linkSize;
			parameters.SpringTension = SpringTension;
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
