using System.Diagnostics;
using Raylib_cs;

namespace im8000emu;

internal class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		if (!BitConverter.IsLittleEndian)
		{
			Console.Error.WriteLine("This emulator does not support big-endian architectures.");
			return;
		}

		if (args.Length < 1)
		{
			Console.Error.WriteLine("Usage: im8000emu <ROM file>");
			return;
		}

		string filePath = args[0].Trim('"').Trim();

		if (!File.Exists(filePath))
		{
			Console.Error.WriteLine($"Could not find file \"{filePath}\"");
			return;
		}

		Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
		Raylib.InitWindow(640, 480, "im8000emu");
		Raylib.SetWindowMinSize(640, 480);

		byte[] romData = File.ReadAllBytes(filePath);
		var system = new EmulatedSystem(romData);

		Console.WriteLine($"im8000emu - {Config.CpuSpeedHz / 1_000_000} MHz, {Config.TargetFramerate} fps target");
		Console.WriteLine($"ROM: {filePath} ({romData.Length:N0} bytes)");
		Console.WriteLine();

		double frameIntervalMs = 1000.0 / Config.TargetFramerate;
		var stopwatch = new Stopwatch();

		while (!Raylib.WindowShouldClose())
		{
			stopwatch.Restart();

			Raylib.BeginDrawing();
			Raylib.ClearBackground(Color.White);

			system.RunFrame();
			Raylib.DrawText("This will eventually do something", 12, 12, 20, Color.DarkPurple);

			Raylib.EndDrawing();

			double elapsedMs = stopwatch.ElapsedMilliseconds;
			double sleepMs = frameIntervalMs - elapsedMs;

			if (sleepMs > 0)
			{
				Thread.Sleep((int)sleepMs);
			}
			else
			{
				Console.WriteLine("Emulator overloaded!");
			}
		}

		Raylib.CloseWindow();
	}
}
