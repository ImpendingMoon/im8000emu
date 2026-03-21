namespace im8000emu.Emulator.Devices;

public interface ISteppingDevice
{
	public static int CpuSpeedHz => Config.CpuSpeedHz;
	public void Step(int cycles);
}
