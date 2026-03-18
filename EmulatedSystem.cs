using im8000emu.Emulator;
using im8000emu.Emulator.Devices;

namespace im8000emu;

internal class EmulatedSystem
{
	private readonly int _cyclesPerFrame = Config.CpuSpeedHz / Config.TargetFramerate;
	private readonly MemoryBus _ioBus;
	private readonly MemoryBus _memoryBus;
	private readonly InterruptBus _interruptBus;

	// Cycles remaining from the previous frame.
	private int _cycleRemainder;

	public EmulatedSystem(byte[] romData)
	{
		// 1980-era business micro, similar to the 5150.

		_memoryBus = new MemoryBus();
		// BIOS ROM mapped to 0x00_0000-0x00_FFFF
		// BIOS extension ROMs follow with 64KB blocks until 0x1F_FFFF
		_memoryBus.Map(0x00_0000, new MemoryDevice(romData, 0x10000, true));
		// At least 16 KB RAM mapped to 0x20_0000-0x3F_0000
		_memoryBus.Map(0x20_0000, new MemoryDevice(Config.MemorySizeKiB));
		// At least 4 KB VRAM mapped to 0xE0_0000-0xFF_FFFF
		_memoryBus.Map(0xE0_0000, new MemoryDevice(0x1000));

		_ioBus = new MemoryBus();
		// Temp, console device maps directly to stdin/out byte streams
		_ioBus.Map(0x00_0000, new ConsoleDevice());

		// TODO:
		// Z80 CTC, SIO, PIO, DMA (maybe 8257 for FDC compat?)
		// NEC uPD765A FDC, MC6845-based video card
		// PC-AT keyboard logic
		_interruptBus = new InterruptBus();

		CPU = new CPU(_memoryBus, _ioBus, _interruptBus);
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
