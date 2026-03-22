using im8000emu.Emulator;
using im8000emu.Emulator.Devices;
using Raylib_cs;

namespace im8000emu;

internal class EmulatedSystem
{
	private readonly int _cyclesPerFrame = Config.CpuSpeedHz / Constants.TargetFramerate;
	private readonly VideoDevice _videoCard;
	private readonly KeyboardDevice _keyboard;

	// Cycles remaining from the previous frame.
	private int _cycleRemainder;

	public EmulatedSystem(byte[] romData)
	{

		var biosRom = new MemoryDevice(romData, 0x10000, true);
		// 1980-era business micro, similar to the 5150.
		var mainRam = new MemoryDevice(Config.MemorySize);

		var memoryBus = new MemoryBus();
		// BIOS ROM mapped to 0x00_0000-0x00_FFFF
		// BIOS extension ROMs follow in 64KB blocks until 0x1F_FFFF
		memoryBus.AttachDevice(biosRom, 0x00_0000, 0x00_FFFF);
		// At least 16 KB RAM
		memoryBus.AttachDevice(mainRam, 0x20_0000, 0x3F_FFFF);

		_videoCard = new VideoDevice(memoryBus);
		_keyboard = new KeyboardDevice();

		var ioBus = new MemoryBus();
		ioBus.AttachDevice(_videoCard, 0x00, 0x03);
		ioBus.AttachDevice(_keyboard, 0x04, 0x07);

		// TODO:
		// - Z80 CTC, SIO, PIO, DMA
		// - NEC uPD765A-compatible FDC, MC6845-based video card
		// - New PIC to interface external interrupt sources with IM 2 bus
		var interruptBus = new InterruptBus();

		CPU = new CPU(memoryBus, ioBus, interruptBus);
		CPU.Reset();
	}

	public Image Frame => _videoCard.GetFrame();

	public CPU CPU { get; }

	public void RunFrame()
	{
		// Read new keys
		_keyboard.Refresh();

		int budget = _cyclesPerFrame + _cycleRemainder;

		while (budget > 0)
		{
			int cycles;

			try
			{
				DecodedOperation operation = CPU.Decode();
				cycles = CPU.Execute(operation);
			}
			catch (EmulatedMachineException ex)
			{
				Console.Error.WriteLine($"Exception during execution: {ex.Message}");

				// No real behavior to emulate. No good way to recover from this.
				// Invalid instructions are a hard crash.
				if (ex is DecodeException)
				{
					throw;
				}

				cycles = 4;
			}

			budget -= cycles;
		}

		_cycleRemainder = budget;
	}
}
