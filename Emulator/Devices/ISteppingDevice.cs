namespace im8000emu.Emulator.Devices;

public interface ISteppingDevice
{
	public static int CpuSpeedHz => Config.CpuSpeedHz;

	/// <summary>
	///     Steps the number of cycles the previous CPU instruction took
	/// </summary>
	/// <param name="cycles"></param>
	public void Step(int cycles);
}
