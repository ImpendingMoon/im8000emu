namespace im8000emu.Emulator.Devices;

public interface ISteppingDevice
{
	public static int CpuSpeedHz => Config.CpuSpeedHz;
	public static int TargetFramerate => Config.TargetFramerate;
	public void Step(int cycles);
}
