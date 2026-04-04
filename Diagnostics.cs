using im8000emu.Emulator;

namespace im8000emu;

/// <summary>
///     A bug in the emulator itself
/// </summary>
internal class EmulatorFaultException : Exception
{
	public EmulatorFaultException(string message) : base(message)
	{
	}

	public EmulatorFaultException(string message, Exception inner) : base(message, inner)
	{
	}
}

/// <summary>
///     Snapshot of the CPU state
/// </summary>
internal readonly struct CpuContext
{
	public uint PC { get; init; }
	public string RegisterDump { get; init; }
	public string FlagDump { get; init; }

	public override string ToString()
	{
		return $"PC=0x{PC:X8}\n  {RegisterDump}\n  {FlagDump}";
	}
}

/// <summary>
///     Base class for faults inside the emulated machine, not in the emulator itself
/// </summary>
internal abstract class CpuException : Exception
{
	protected CpuException(string message, CpuContext context) : base($"{message}\n  {context}")
	{
		Context = context;
	}

	public CpuContext Context { get; }
}

internal class IllegalInstructionException : CpuException
{
	public IllegalInstructionException(uint pc, ushort instructionWord, string reason, CpuContext context) : base(
		$"Illegal instruction at PC=0x{pc:X8} [0x{instructionWord:X4}]: {reason}",
		context
	)
	{
		InstructionWord = instructionWord;
	}

	public IllegalInstructionException(uint pc, string reason, CpuContext context) : base(
		$"Illegal instruction at PC=0x{pc:X8}: {reason}",
		context
	)
	{
		InstructionWord = null;
	}

	public ushort? InstructionWord { get; }
}

internal class MemoryFaultException : CpuException
{
	public MemoryFaultException(uint address, Constants.DataSize size, bool isWrite, string reason, CpuContext context)
		: base($"Memory fault: {(isWrite ? "write" : "read")} of {size} at 0x{address:X8}: {reason}", context)
	{
		Address = address;
		Size = size;
		IsWrite = isWrite;
	}

	public uint Address { get; }
	public Constants.DataSize Size { get; }
	public bool IsWrite { get; }
}

public sealed class DeviceException : Exception
{
	public DeviceException(uint offset, Constants.DataSize size, bool isWrite, string reason) : base(
		$"{(isWrite ? "write" : "read")} of {size} at device offset 0x{offset:X}: {reason}"
	)
	{
		Offset = offset;
		Size = size;
		IsWrite = isWrite;
		Reason = reason;
	}

	public uint Offset { get; }
	public Constants.DataSize Size { get; }
	public bool IsWrite { get; }
	public string Reason { get; }
}

internal class ExecutionFaultException : CpuException
{
	public ExecutionFaultException(string reason, CpuContext context) : base($"Execution fault: {reason}", context)
	{
	}
}
