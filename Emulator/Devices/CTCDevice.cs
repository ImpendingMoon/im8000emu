namespace im8000emu.Emulator.Devices;

public class CTCDevice : IMemoryDevice, ISteppingDevice, IInterruptingDevice
{
	public bool INT { get; }
	public bool NMI { get; }
	public bool IsServicingInterrupt { get; }

	public byte OnInterruptAcknowledge()
	{
		throw new NotImplementedException();
	}

	public void OnInterruptComplete()
	{
		throw new NotImplementedException();
	}

	public uint Size { get; }

	public uint Read(uint address, Constants.DataSize size)
	{
		throw new NotImplementedException();
	}

	public void Write(uint address, Constants.DataSize size, uint value)
	{
		throw new NotImplementedException();
	}

	public int Step(int cycles)
	{
		throw new NotImplementedException();
	}
}
