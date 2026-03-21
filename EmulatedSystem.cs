using im8000emu.Emulator;
using im8000emu.Emulator.Devices;

namespace im8000emu;

internal class EmulatedSystem
{
	private readonly int _cyclesPerFrame = Config.CpuSpeedHz / Config.TargetFramerate;

	// Cycles remaining from the previous frame.
	private int _cycleRemainder;

	public EmulatedSystem(byte[] romData)
	{

		var biosRom = new MemoryDevice(romData, 0x10000, true);
		// 1980-era business micro, similar to the 5150.
		var mainRam = new MemoryDevice(Config.MemorySize);
		var videoRam = new MemoryDevice(Config.VideoMemorySize);

		var memoryBus = new MemoryBus();
		// BIOS ROM mapped to 0x00_0000-0x00_FFFF
		// BIOS extension ROMs follow in 64KB blocks until 0x1F_FFFF
		memoryBus.AttachDevice(biosRom, 0x00_0000, 0x00_FFFF);
		// At least 16 KB RAM
		memoryBus.AttachDevice(mainRam, 0x20_0000, 0x3F_FFFF);
		// Unused from 0x40_0000-0xDF_FFFF
		// At least 4 KB VRAM
		memoryBus.AttachDevice(videoRam, 0xE0_0000, 0xFF_FFFF);

		var ioBus = new MemoryBus();
		// Temp, console device maps directly to stdin/out byte streams
		ioBus.AttachDevice(new ConsoleDevice(), 0, 4);

		// TODO:
		// - Z80 CTC, SIO, PIO, DMA
		// - NEC uPD765A-compatible FDC, MC6845-based video card
		// - New PIC to interface external interrupt sources with IM 2 bus
		// - PC-AT keyboard logic. Probably just send scancodes in circular buffer.
		//   This was a generic MCU on the PC-AT anyway.
		var interruptBus = new InterruptBus();

		CPU = new CPU(memoryBus, ioBus, interruptBus);
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
				uint pc = CPU.Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
				pc += 2;
				CPU.Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, pc);

				cycles = 1;
			}

			budget -= cycles;
		}

		_cycleRemainder = budget;
	}
}
