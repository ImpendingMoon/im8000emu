using im8000emu.Emulator;
using im8000emu.Emulator.Devices;
using Raylib_cs;

namespace im8000emu;

internal class EmulatedSystem
{
	private readonly int _cyclesPerFrame = Config.CpuSpeedHz / Constants.TargetFramerate;
	private readonly KeyboardDevice _keyboard;
	private readonly List<ISteppingDevice> _steppingDevices;
	private readonly VideoDevice _videoCard;

	// Cycles remaining from the previous frame.
	private int _cycleRemainder;

	// 1980-era business micro, similar to the 5150.
	public EmulatedSystem(byte[] romData)
	{
		var biosRom = new MemoryDevice(romData, 0x10000, true);
		var mainRam = new MemoryDevice(Config.MemorySize);

		var memoryBus = new MemoryBus(0);
		// BIOS ROM mapped to 0x00_0000-0x00_FFFF
		// BIOS extension ROMs follow in 256KB blocks until 0x1F_FFFF
		memoryBus.AttachDevice(biosRom, 0x00_0000, 0x00_FFFF);
		memoryBus.AttachDevice(mainRam, 0x20_0000, 0x3F_FFFF);

		_videoCard = new VideoDevice(memoryBus);
		_keyboard = new KeyboardDevice();

		var ioBus = new MemoryBus(Config.IOCycleCost - Config.BusCycleCost);
		ioBus.AttachDevice(_keyboard, 0x20, 0x3F);
		ioBus.AttachDevice(_videoCard, 0x60, 0x7F);

		// TODO:
		// - Z80 CTC, SIO, PIO, DMA
		// - NEC uPD765A-compatible FDC, MC6845-based video card
		// - New PIC to interface external interrupt sources with IM 2 bus
		var interruptBus = new InterruptBus();
		interruptBus.AttachDevice(_keyboard, 1);

		CPU = new CPU(memoryBus, ioBus, interruptBus);
		CPU.Reset();

		_steppingDevices = [];
	}

	public Image Frame => _videoCard.GetFrame();
	public CPU CPU { get; }

	public bool Paused { get; private set; }

	/// <summary>The exception that caused the most recent pause</summary>
	public CpuException? MostRecentException { get; private set; }

	public void Resume()
	{
		MostRecentException = null;
		Paused = false;
	}

	public void RunFrame()
	{
		if (Paused)
		{
			return;
		}

		_keyboard.Refresh();

		int budget = _cyclesPerFrame + _cycleRemainder;

		while (budget > 0)
		{
			int cycles;

			try
			{
				DecodedOperation operation = CPU.Decode();
				cycles = CPU.Execute(operation);

				foreach (ISteppingDevice device in _steppingDevices)
				{
					cycles += device.Step(cycles);
				}
			}
			catch (CpuException ex)
			{
				Paused = true;
				MostRecentException = ex;

				Console.Error.WriteLine();
				Console.Error.WriteLine(ex.Message);
				return;
			}

			budget -= cycles;
		}

		_cycleRemainder = budget;
	}
}
