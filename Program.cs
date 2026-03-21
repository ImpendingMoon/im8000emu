using System.Diagnostics;
using System.Numerics;
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
		Raylib.InitWindow(640 * 2, 480 * 2, "im8000emu");
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

			system.RunFrame();

			Texture2D texture = Raylib.LoadTextureFromImage(system.Frame);

			float scaleX = Raylib.GetScreenWidth() / (float)texture.Width;
			float scaleY = Raylib.GetScreenHeight() / (float)texture.Height;

			var src = new Rectangle(0, 0, texture.Width, texture.Height);
			var dst = new Rectangle(
				(Raylib.GetScreenWidth() - (texture.Width * scaleX)) / 2f,
				(Raylib.GetScreenHeight() - (texture.Height * scaleY)) / 2f,
				texture.Width * scaleX,
				texture.Height * scaleY
			);

			Raylib.BeginDrawing();
			Raylib.ClearBackground(Color.White);
			Raylib.DrawTexturePro(texture, src, dst, Vector2.Zero, 0f, Color.White);
			Raylib.EndDrawing();

			Raylib.UnloadTexture(texture);

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
