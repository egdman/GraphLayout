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
	//class Calculator
	//{
	//	[StructLayout(LayoutKind.Explicit, Size = 144)]
	//	struct Params
	//	{
	//		[FieldOffset(0)]
	//		public Matrix View;
	//		[FieldOffset(64)]
	//		public Matrix Projection;
	//		[FieldOffset(128)]
	//		public uint MaxParticles;
	//		[FieldOffset(132)]
	//		public float StepLength;
	//		[FieldOffset(136)]
	//		public float LinkSize;
	//	}

	//	enum Flags
	//	{
	//		COMPUTE		= 0x1,
	//		INJECTION	= 0x1 << 1,
	//		SIMULATION	= 0x1 << 2,
	//		MOVE		= 0x1 << 3,
	//		REDUCTION	= 0x1 << 4,
	//		EULER		= 0x1 << 5,
	//		RUNGE_KUTTA	= 0x1 << 6,	
	//		LINKS		= 0x1 << 7
	//	}
		
	//	// size of a thread block:
	//	const int BlockSize = 256;

	//	ConstantBuffer		paramsCB;

	//	StructuredBuffer	linksBuffer;
	//	StructuredBuffer	linksPtrBuffer;

	//	Ubershader		shader;
	//	StateFactory	factory;



	//	public Calculator( GraphicsDevice device )
	//	{

	//	}


	//	/// <summary>
	//	/// This function calculates two values:
	//	/// 1. Energy E for every vertex				(N floats)
	//	/// 2. Descent vector which equals -grad(E)		(3*N floats)
	//	/// Values are overwritten into the same buffer
	//	/// </summary>
	//	/// <param name="device"></param>
	//	/// <param name="rwVertexBuffer"></param>
	//	/// <param name="parameters"></param>
	//	void calcDescentVector(GraphicsDevice device, StructuredBuffer rwVertexBuffer, Params parameters)
	//	{
	//		paramsCB.SetData(parameters);
	//		device.ComputeShaderConstants[0] = paramsCB;
	//		device.SetCSRWBuffer(0, rwVertexBuffer, (int)parameters.MaxParticles);
	//		device.ComputeShaderResources[2] = linksPtrBuffer;
	//		device.ComputeShaderResources[3] = linksBuffer;
	//		device.PipelineState = factory[(int)(Flags.COMPUTE | Flags.SIMULATION | Flags.EULER | Flags.LINKS)];
	//		device.Dispatch(MathUtil.IntDivUp((int)parameters.MaxParticles, BlockSize));
	//		device.ResetStates();
	//	}



	//	/// <summary>
	//	/// This function takes vertices from the source buffer and moves them by
	//	/// descent vector times stepLength (Pk*stepLength).
	//	/// Then it writes them into the destination buffer with new positions.
	//	/// This function does not change data in the source buffer.
	//	/// </summary>
	//	/// <param name="device"></param>
	//	/// <param name="srcVertexBuffer"></param>
	//	/// <param name="dstVertexBuffer"></param>
	//	/// <param name="parameters"></param>
	//	void moveVertices(GraphicsDevice device, StructuredBuffer srcVertexBuffer,
	//						StructuredBuffer dstVertexBuffer, Params parameters)
	//	{
	//		paramsCB.SetData(parameters);
	//		device.ComputeShaderConstants[0] = paramsCB;
	//		device.ComputeShaderResources[1] = srcVertexBuffer;
	//		device.SetCSRWBuffer(0, dstVertexBuffer, (int)parameters.MaxParticles);
	//		device.PipelineState = factory[(int)(Flags.COMPUTE | Flags.MOVE)];
	//		device.Dispatch(MathUtil.IntDivUp((int)parameters.MaxParticles, BlockSize));
	//		device.ResetStates();
	//	}



	//	/// <summary>
	//	/// This function calculates the following values:
	//	/// 1. Total energy at (k+1)th iteration (from nextStateBuffer).
	//	/// 2. Dot product of kth descent vector and (k+1)th energy gradient (which equals minus (k+1)th descent vector)
	//	/// </summary>
	//	/// <param name="device"></param>
	//	/// <param name="currentStateBuffer"></param>
	//	/// <param name="nextStateBuffer"></param>
	//	/// <param name="outputValues"></param>
	//	/// <param name="parameters"></param>
	//	/// <param name="energy"></param>
	//	/// <param name="pTgradE"></param>
	//	void calcTotalEnergyAndDotProduct(GraphicsDevice device, StructuredBuffer currentStateBuffer,
	//		StructuredBuffer nextStateBuffer, StructuredBuffer outputValues, Params parameters,
	//		out float energy, out float pTgradE, out float checkSum)
	//	{
	//		// preform reduction on GPU:
	//		paramsCB.SetData(parameters);
	//		device.ComputeShaderConstants[0] = paramsCB;
	//		device.ComputeShaderResources[1] = currentStateBuffer;
	//		device.ComputeShaderResources[4] = nextStateBuffer;
	//		device.SetCSRWBuffer(1, outputValues, MathUtil.IntDivUp((int)parameters.MaxParticles, BlockSize));
	//		device.PipelineState = factory[(int)Flags.COMPUTE | (int)Flags.REDUCTION];
	//		device.Dispatch(MathUtil.IntDivUp((int)parameters.MaxParticles, BlockSize));

	//		// perform final summation:
	//		Vector4[] valueBufferCPU = new Vector4[outputValues.GetStructureCount()];
	//		outputValues.GetData(valueBufferCPU);
	//		energy = 0;
	//		pTgradE = 0;
	//		checkSum = 0;
	//		foreach (var value in valueBufferCPU)
	//		{
	//			energy += value.X;
	//			pTgradE += value.Y;
	//			checkSum += value.Z;
	//		}
	//		energy /= 2; // because each pair is counted 2 times
	//	}

		
	//}
}
