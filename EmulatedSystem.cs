using im8000emu.Emulator;
using im8000emu.Emulator.Devices;

namespace im8000emu;

internal class EmulatedSystem
{
	private readonly int _cyclesPerFrame = Config.CpuSpeedHz / Config.TargetFramerate;
	private readonly MemoryBus _ioBus;
	private readonly MemoryBus _memoryBus;

	// Cycles remaining from the previous frame.
	private int _cycleRemainder;

	public EmulatedSystem(byte[] romData)
	{
		_memoryBus = new MemoryBus();
		_memoryBus.Map(0x0000_0000_0000, new MemoryDevice(romData, romData.Length, true));
		_memoryBus.Map(0x0000_0010_0000, new MemoryDevice(0x10000));

		_ioBus = new MemoryBus();
		_ioBus.Map(0x0000_0000_0000, new ConsoleDevice());

		CPU = new CPU(_memoryBus, _ioBus);
		CPU.Reset();
	}

	public CPU CPU { get; }

	public void RunFrame()
	{
		int budget = _cyclesPerFrame + _cycleRemainder;

		while (budget > 0)
		{
			int cycles;

			try
			{
				DecodedOperation operation = CPU.Decode();
				cycles = CPU.Execute(operation);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"Exception during execution: {ex.Message}");

				// Advance PC to avoid an infinite fault loop.
				uint pc = CPU.Registers.GetRegister(Constants.RegisterTargets.PC, Constants.OperandSize.DWord);
				pc += 2;
				CPU.Registers.SetRegister(Constants.RegisterTargets.PC, Constants.OperandSize.DWord, pc);

				cycles = 1;
			}

			budget -= cycles;
		}

		_cycleRemainder = budget;
	}
}
