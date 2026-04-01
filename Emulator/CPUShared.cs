namespace im8000emu.Emulator;

// Contains shared private members between CPU.cs, CPUDecode.cs, and CPUExecute.cs
internal partial class CPU
{
	private readonly InterruptBus _interruptBus;
	private readonly MemoryBus _ioBus;
	private readonly MemoryBus _memoryBus;

	// Reusable decode object. Avoids a heap allocation on every fetch.
	private DecodedOperation _currentOperation;
	private int _interruptMode = 1;
	private bool _isHalted;
	private bool _shouldEnableInterrupts;

	private MemoryResult ReadMemory(uint address, Constants.DataSize size, bool useIO = false)
	{
		MemoryResult result;

		bool aligned = (address & 1) == 0;

		MemoryBus activeBus = useIO ? _ioBus : _memoryBus;

		result.Value = activeBus.Read(address, size);
		result.Cycles = size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => aligned ? 1 : 2,
			Constants.DataSize.DWord => aligned ? 2 : 3,
			_ => throw new EmulatorException($"ReadMemory is not implemented for DataSize {size}"),
		};

		result.Cycles *= useIO ? Config.IOCycleCost : Config.BusCycleCost;

		return result;
	}

	private MemoryResult WriteMemory(uint address, Constants.DataSize size, uint value, bool useIO = false)
	{
		MemoryResult result;
		result.Value = 0;

		bool aligned = (address & 1) == 0;

		MemoryBus activeBus = useIO ? _ioBus : _memoryBus;

		activeBus.Write(address, size, value);
		result.Cycles = size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => aligned ? 1 : 2,
			Constants.DataSize.DWord => aligned ? 2 : 3,
			_ => throw new EmulatorException($"WriteMemory is not implemented for DataSize {size}"),
		};

		result.Cycles *= useIO ? Config.IOCycleCost : Config.BusCycleCost;

		return result;
	}

	private MemoryResult GetOperandValue(in Operand operand, Constants.DataSize size)
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

	private MemoryResult WritebackOperand(in Operand operand, Constants.DataSize size, uint value)
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
			address = Registers.GetRegister(operand.Target.Value, Constants.DataSize.DWord);
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

	private void ExchangeWithAlternate(Constants.RegisterTargets register, Constants.DataSize size)
	{
		// In hardware, this would just be a mux bit flip
		// In software, easier to actually exchange the values
		Constants.RegisterTargets alternate = Constants.RegisterToAlternate[register];

		uint primaryValue = Registers.GetRegister(register, size);
		uint alternateValue = Registers.GetRegister(alternate, size);

		Registers.SetRegister(alternate, size, primaryValue);
		Registers.SetRegister(register, size, alternateValue);
	}

	private uint FetchImmediate(ref DecodedOperation decodedOperation, Constants.DataSize size)
	{
		MemoryResult immediateFetch = ReadMemory(
			(uint)(decodedOperation.BaseAddress + decodedOperation.OpcodeLength),
			size
		);
		decodedOperation.FetchCycles += immediateFetch.Cycles;
		decodedOperation.OpcodeLength += size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.DWord => 4,
			_ => throw new InvalidSizeException(decodedOperation.BaseAddress, $"{size} is not a valid operand size"),
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
			_ => throw new EmulatorException($"IsConditionTrue is not implemented for condition {condition}"),
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
