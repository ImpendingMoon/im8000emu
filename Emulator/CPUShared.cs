using System.Buffers.Binary;

namespace im8000emu.Emulator;

// Contains shared private members between CPU.cs, CPUDecode.cs, and CPUExecute.cs
internal partial class CPU
{
	private readonly MemoryBus _ioBus;
	private readonly MemoryBus _memoryBus;

	// Reusable decode object. Avoids a heap allocation on every fetch.
	private DecodedOperation _currentOperation;
	private int _interruptMode = 1;
	private bool _isHalted;
	private bool _shouldEnableInterrupts;

	private MemoryResult ReadMemory(uint address, Constants.OperandSize size, bool useIO = false)
	{
		MemoryResult result;

		bool aligned = (address & 1) == 0;

		MemoryBus activeBus = useIO ? _ioBus : _memoryBus;

		switch (size)
		{
			case Constants.OperandSize.Byte:
			{
				result.Value = activeBus.ReadByte(address);
				result.Cycles = 1;
				break;
			}

			case Constants.OperandSize.Word:
			{
				Span<byte> data = activeBus.ReadByteArray(address, 2);
				result.Value = BinaryPrimitives.ReadUInt16LittleEndian(data);
				result.Cycles = aligned ? 1 : 2;
				break;
			}

			case Constants.OperandSize.DWord:
			{
				Span<byte> data = activeBus.ReadByteArray(address, 4);
				result.Value = BinaryPrimitives.ReadUInt32LittleEndian(data);
				result.Cycles = aligned ? 2 : 3;
				break;
			}

			default:
			{
				throw new ArgumentException($"ReadMemory is not implemented for OperandSize {size}");
			}
		}

		result.Cycles *= useIO ? Config.IOCycleCost : Config.BusCycleCost;

		return result;
	}

	private MemoryResult WriteMemory(uint address, Constants.OperandSize size, uint value, bool useIO = false)
	{
		MemoryResult result;
		result.Value = 0;

		bool aligned = (address & 1) == 0;

		MemoryBus activeBus = useIO ? _ioBus : _memoryBus;

		switch (size)
		{
			case Constants.OperandSize.Byte:
			{
				activeBus.WriteByte(address, (byte)value);
				result.Cycles = 1;
				break;
			}

			case Constants.OperandSize.Word:
			{
				Span<byte> bytes = stackalloc byte[2];
				BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)value);
				activeBus.WriteByteArray(address, bytes);
				result.Cycles = aligned ? 1 : 2;
				break;
			}

			case Constants.OperandSize.DWord:
			{
				Span<byte> bytes = stackalloc byte[4];
				BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
				activeBus.WriteByteArray(address, bytes);
				result.Cycles = aligned ? 2 : 3;
				break;
			}

			default:
			{
				throw new ArgumentException($"WriteMemory is not implemented for OperandSize {size}");
			}
		}

		result.Cycles *= useIO ? Config.IOCycleCost : Config.BusCycleCost;

		return result;
	}

	private MemoryResult GetOperandValue(in Operand operand, Constants.OperandSize size)
	{
		if (operand.Target is null && operand.Immediate is null)
		{
			throw new ArgumentException("Either Target or Immediate must have a value");
		}

		MemoryResult result;

		if (operand.Indirect)
		{
			uint address = GetEffectiveAddress(in operand);
			result = ReadMemory(address, size);
		}
		else
		{
			if (operand.Target is not null)
			{
				result.Value = Registers.GetRegister(operand.Target.Value, size);
			}
			else
			{
				result.Value = operand.Immediate!.Value;
			}

			result.Cycles = 0;
		}

		return result;
	}

	private MemoryResult WritebackOperand(in Operand operand, Constants.OperandSize size, uint value)
	{
		if (operand.Target is null && operand.Immediate is null)
		{
			throw new ArgumentException("Either Target or Immediate must have a value");
		}

		MemoryResult result;

		if (operand.Indirect)
		{
			uint address = GetEffectiveAddress(in operand);
			result = WriteMemory(address, size, value);
		}
		else
		{
			if (operand.Target is null)
			{
				throw new ArgumentException("Cannot writeback to an immediate operand");
			}

			Registers.SetRegister(operand.Target.Value, size, value);
			result.Value = 0;
			result.Cycles = 0;
		}

		return result;
	}

	private uint GetEffectiveAddress(in Operand operand)
	{
		uint address;

		if (operand.Target is not null)
		{
			address = Registers.GetRegister(operand.Target.Value, Constants.OperandSize.DWord);
		}
		else if (operand.Immediate is not null)
		{
			address = operand.Immediate.Value;
		}
		else
		{
			throw new ArgumentException("Either Target or Immediate must have a value");
		}

		if (operand.Displacement is not null)
		{
			address = (uint)((int)address + operand.Displacement.Value);
		}

		return address;
	}

	private void ExchangeWithAlternate(Constants.RegisterTargets register, Constants.OperandSize size)
	{
		// In hardware, this would just be a mux bit flip
		// In software, easier to actually exchange the values
		Constants.RegisterTargets alternate = Constants.RegisterToAlternate[register];

		ushort primaryValue = (ushort)Registers.GetRegister(register, Constants.OperandSize.Word);
		ushort alternateValue = (ushort)Registers.GetRegister(alternate, Constants.OperandSize.Word);

		Registers.SetRegister(alternate, Constants.OperandSize.Word, primaryValue);
		Registers.SetRegister(register, Constants.OperandSize.Word, alternateValue);
	}

	// ref parameter: DecodedOperation is now a struct, so we must pass by ref to mutate FetchCycles/OpcodeLength
	private uint FetchImmediate(ref DecodedOperation decodedOperation, Constants.OperandSize size)
	{
		MemoryResult immediateFetch = ReadMemory(
			(uint)(decodedOperation.BaseAddress + decodedOperation.OpcodeLength),
			size
		);
		decodedOperation.FetchCycles += immediateFetch.Cycles;
		decodedOperation.OpcodeLength += size switch
		{
			Constants.OperandSize.Byte => 1,
			Constants.OperandSize.Word => 2,
			Constants.OperandSize.DWord => 4,
			_ => throw new ArgumentException($"{size} is not a valid operand size"),
		};
		return immediateFetch.Value;
	}

	private bool IsConditionTrue(Constants.Condition condition)
	{
		return condition switch
		{
			Constants.Condition.NZ => !Registers.GetFlag(Constants.FlagMasks.Zero),
			Constants.Condition.Z => Registers.GetFlag(Constants.FlagMasks.Zero),
			Constants.Condition.NC => !Registers.GetFlag(Constants.FlagMasks.Carry),
			Constants.Condition.C => Registers.GetFlag(Constants.FlagMasks.Carry),
			Constants.Condition.PO => !Registers.GetFlag(Constants.FlagMasks.ParityOverflow),
			Constants.Condition.PE => Registers.GetFlag(Constants.FlagMasks.ParityOverflow),
			Constants.Condition.P => !Registers.GetFlag(Constants.FlagMasks.Sign),
			Constants.Condition.N => Registers.GetFlag(Constants.FlagMasks.Sign),
			Constants.Condition.Unconditional => true,
			_ => throw new ArgumentException($"IsConditionTrue is not implemented for condition {condition}"),
		};
	}

	private void UpdateALUFlags(ALUFlagState state)
	{
		Registers.SetFlag(Constants.FlagMasks.Carry, state.Carry);
		Registers.SetFlag(Constants.FlagMasks.Subtract, state.Subtract);
		Registers.SetFlag(Constants.FlagMasks.ParityOverflow, state.ParityOverflow);
		Registers.SetFlag(Constants.FlagMasks.HalfCarry, state.HalfCarry);
		Registers.SetFlag(Constants.FlagMasks.Zero, state.Zero);
		Registers.SetFlag(Constants.FlagMasks.Sign, state.Sign);
	}

	private ALUFlagState GetALUFlags()
	{
		return new ALUFlagState
		{
			Carry = Registers.GetFlag(Constants.FlagMasks.Carry),
			Subtract = Registers.GetFlag(Constants.FlagMasks.Subtract),
			ParityOverflow = Registers.GetFlag(Constants.FlagMasks.ParityOverflow),
			HalfCarry = Registers.GetFlag(Constants.FlagMasks.HalfCarry),
			Zero = Registers.GetFlag(Constants.FlagMasks.Zero),
			Sign = Registers.GetFlag(Constants.FlagMasks.Sign),
		};
	}

	private struct MemoryResult
	{
		public uint Value;
		public int Cycles;
	}

	private struct ALUFlagState
	{
		public bool Carry;
		public bool Subtract;
		public bool ParityOverflow;
		public bool HalfCarry;
		public bool Zero;
		public bool Sign;
	}
}
