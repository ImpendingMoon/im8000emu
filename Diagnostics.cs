using im8000emu.Emulator;

namespace im8000emu;

/// <summary>
///     Issues with the emulator itself
/// </summary>
internal class EmulatorException : Exception
{
	public EmulatorException(string message) : base(message)
	{
	}

	public EmulatorException(string message, Exception inner) : base(message, inner)
	{
	}
}

/// <summary>
///     Issues with software in the emulated machine
/// </summary>
internal class EmulatedMachineException : Exception
{
	public EmulatedMachineException(string message) : base(message)
	{
	}

	public EmulatedMachineException(string message, Exception inner) : base(message, inner)
	{
	}
}

internal class InvalidExecutorOperandException : EmulatorException
{
	public InvalidExecutorOperandException(string message) : base($"Invalid executor operand: {message}")
	{
	}
}

internal class DecodeException : EmulatedMachineException
{
	public DecodeException(uint pc, string reason) : base($"Decode failure at 0x{pc:X}: {reason}")
	{

	}

	public DecodeException(uint pc, uint instructionWord, string reason) : base(
		$"Decode failure at PC=0x{pc:X}: [0x{instructionWord:X}] {reason}"
	)
	{
		PC = pc;
		InstructionWord = instructionWord;
	}

	public uint InstructionWord { get; }
	public uint PC { get; }
}

internal class InvalidSizeException : DecodeException
{
	public InvalidSizeException(uint pc, string message) : base(pc, message)
	{
	}
}

internal class MemoryAccessException : EmulatedMachineException
{
	public MemoryAccessException(uint address, Constants.DataSize size, bool isWrite) : base(
		$"{(isWrite ? "Write" : "Read")} of size {size} at invalid address 0x{address:X}"
	)
	{
		Address = address;
		Size = size;
		IsWrite = isWrite;
	}

	public uint Address { get; }
	public Constants.DataSize Size { get; }
	public bool IsWrite { get; }

	internal class ReadOnlyViolationException : EmulatedMachineException
	{
		public ReadOnlyViolationException(uint address) : base($"Attempted write to read-only address 0x{address:X}")
		{
		}
	}

	internal class WriteOnlyViolationException : EmulatedMachineException
	{
		public WriteOnlyViolationException(uint address) : base($"Attempted read from write-only address 0x{address:X}")
		{
		}
	}
}
