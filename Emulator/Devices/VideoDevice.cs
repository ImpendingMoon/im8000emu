using Raylib_cs;

namespace im8000emu.Emulator.Devices;

/// <summary>
///     Implements a basic Video Device, mapping VRAM to 0xC0_0000 and providing itself as an I/O device
///     Address map:
///     0 - MODE (read/write)
///     - 0x00 = Text 80x25
///     - 0x10 = Monochrome Graphics 640x200 (requires 16KB VRAM)
///     - 0x11 = 4-Color Graphics 320x200 (requires 16KB VRAM)
///     1 - CONFIG
///     - b0 = Display Disable (0)/Enable (1)
///     - b1 = Cursor Disable (0)/Enable (1)
///     2 - CURSOR ROW
///     - Must be between 0-24
///     3 - CURSOR COLUMN
///     - Must be between 0-79
/// </summary>
internal class VideoDevice : IMemoryDevice
{
	private static readonly Color[] CgaPalette =
	[
		new(0, 0, 0, 255), // 0 black
		new(0, 0, 170, 255), // 1 blue
		new(0, 170, 0, 255), // 2 green
		new(0, 170, 170, 255), // 3 cyan
		new(170, 0, 0, 255), // 4 red
		new(170, 0, 170, 255), // 5 magenta
		new(170, 85, 0, 255), // 6 brown
		new(170, 170, 170, 255), // 7 light gray
		new(85, 85, 85, 255), // 8 dark gray
		new(85, 85, 255, 255), // 9 bright blue
		new(85, 255, 85, 255), // 10 bright green
		new(85, 255, 255, 255), // 11 bright cyan
		new(255, 85, 85, 255), // 12 bright red
		new(255, 85, 255, 255), // 13 bright magenta
		new(255, 255, 85, 255), // 14 yellow
		new(255, 255, 255, 255), // 15 white
	];
	private readonly Image _blankDisplay;
	private readonly Image _font;
	private readonly MemoryDevice _vram;
	private int _currentFrameCycle; // Controls cursor blinking 0-29 - off, 30-59 - on
	private int _cursorColumn;
	private int _cursorRow;
	private Image _display;
	private bool _enableCursor;
	private bool _enableDisplay;
	private Constants.VideoMode _mode;

	public VideoDevice(MemoryBus memoryBus)
	{
		if (Config.VideoMemorySize < 4096)
		{
			throw new EmulatorException($"Not enough VRAM! Need at least 4096 bytes, got {Config.VideoMemorySize}.");
		}
		_vram = new MemoryDevice(Config.VideoMemorySize);
		_mode = Constants.VideoMode.Text80x25;
		_display = Raylib.GenImageColor(640, 200, Color.Black);
		_blankDisplay = Raylib.GenImageColor(1, 1, Color.Black);
		string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "font.png");
		_font = Raylib.LoadImage(fontPath);
		_cursorRow = 0;
		_cursorColumn = 0;
		_enableDisplay = false;
		_enableCursor = false;
		_currentFrameCycle = 0;
		memoryBus.AttachDevice(_vram, 0xE0_0000, 0xFF_FFFF);
	}

	public uint Size => 4;

	public uint Read(uint address, Constants.DataSize size)
	{
		uint offset = address % Size;

		if (size != Constants.DataSize.Byte)
		{
			if (Config.EnableStrictMode)
			{
				throw new EmulatedMachineException($"Cannot access video registers with {size} size!");
			}

			if (size == Constants.DataSize.Word)
			{
				uint value = Read(offset, Constants.DataSize.Byte);
				value |= Read(offset + 1, Constants.DataSize.Byte) << 8;
				return value;
			}
			else
			{
				uint value = Read(offset, Constants.DataSize.Byte);
				value |= Read(offset + 1, Constants.DataSize.Byte) << 8;
				value |= Read(offset + 2, Constants.DataSize.Byte) << 16;
				value |= Read(offset + 3, Constants.DataSize.Byte) << 24;
				return value;
			}
		}

		return offset switch
		{
			0 => (uint)_mode,
			1 => GetConfig(),
			2 => (uint)_cursorRow,
			3 => (uint)_cursorColumn,
			_ => 0xFF,
		};
	}

	public void Write(uint address, Constants.DataSize size, uint value)
	{
		uint offset = address % Size;

		if (size != Constants.DataSize.Byte)
		{
			if (Config.EnableStrictMode)
			{
				throw new EmulatedMachineException($"Cannot access video registers with {size} size!");
			}

			if (size == Constants.DataSize.Word)
			{
				Write(offset, Constants.DataSize.Byte, value);
				Write(offset + 1, Constants.DataSize.Byte, value >> 8);
			}
			else
			{
				Write(offset, Constants.DataSize.Byte, value);
				Write(offset + 1, Constants.DataSize.Byte, value >> 8);
				Write(offset + 2, Constants.DataSize.Byte, value >> 16);
				Write(offset + 3, Constants.DataSize.Byte, value >> 24);
			}
		}

		switch (offset)
		{
			case 0: SetMode(value); break;
			case 1: SetConfig(value); break;
			case 2: _cursorRow = (int)value; break;
			case 3: _cursorColumn = (int)value; break;
		}
	}

	~VideoDevice()
	{
		Raylib.UnloadImage(_font);
		Raylib.UnloadImage(_display);
		Raylib.UnloadImage(_blankDisplay);
	}

	public Image GetFrame()
	{
		if (!_enableDisplay)
		{
			return _blankDisplay;
		}

		switch (_mode)
		{
			case Constants.VideoMode.Text80x25:
				RenderText80x25Mode();
				break;
			case Constants.VideoMode.Graphics640x200x1bpp:
				RenderMonochrome640x200Mode();
				break;
			case Constants.VideoMode.Graphics320x200x2bpp:
				RenderMonochrome640x200Mode();
				break;
			default:
				throw new NotImplementedException($"Video Mode {_mode} is unimplemented");
		}

		_currentFrameCycle += 1;
		if (_currentFrameCycle == 60)
		{
			_currentFrameCycle = 0;
		}

		return _display;
	}

	private void SetMode(uint value)
	{
		if (!Enum.IsDefined(typeof(Constants.VideoMode), value))
		{
			if (Config.EnableStrictMode)
			{
				throw new EmulatedMachineException($"{value} is not a valid video mode");
			}

			_mode = default;
		}
		else
		{
			_mode = (Constants.VideoMode)value;

			if (Config.EnableStrictMode && _mode != Constants.VideoMode.Text80x25 && _vram.Size < 16384)
			{
				_mode = Constants.VideoMode.Text80x25;
				throw new EmulatedMachineException(
					$"Not enough VRAM to enable graphics mode! Need at least 16384 bytes, have {_vram.Size}"
				);
			}
		}
	}

	private void SetConfig(uint value)
	{
		_enableDisplay = (value & 1) != 0;
		_enableCursor = (value & 2) != 0;
	}

	private byte GetConfig()
	{
		byte value = (byte)(_enableDisplay ? 1 : 0);
		value |= (byte)(_enableCursor ? 2 : 0);
		return value;
	}

	private void RenderText80x25Mode()
	{
		const int columns = 80;
		const int rows = 25;
		const int glyphWidth = 8;
		const int glyphHeight = 8;

		if (_display.Width != columns * glyphWidth || _display.Height != rows * glyphHeight)
		{
			Raylib.UnloadImage(_display);
			_display = Raylib.GenImageColor(columns * glyphWidth, rows * glyphHeight, Color.Black);
		}

		for (int row = 0; row < rows; row++)
		{
			for (int col = 0; col < columns; col++)
			{
				uint index = (uint)((row * columns) + col) * 2;

				byte charCode = (byte)_vram.Read(index, Constants.DataSize.Byte);
				byte attribute = (byte)_vram.Read(index + 1, Constants.DataSize.Byte);

				int fgColorIndex = attribute & 0x0F;
				int bgColorIndex = (attribute >> 4) & 0x07;

				Color fg = CgaPalette[fgColorIndex];
				Color bg = CgaPalette[bgColorIndex];

				int glyphCol = charCode % 32;
				int glyphRow = charCode / 32;

				var src = new Rectangle(glyphCol * glyphWidth, glyphRow * glyphHeight, glyphWidth, glyphHeight);
				var dst = new Rectangle(col * glyphWidth, row * glyphHeight, glyphWidth, glyphHeight);

				// Draw background color
				Raylib.ImageDrawRectangleRec(ref _display, dst, bg);
				// Draw glyph
				Raylib.ImageDraw(ref _display, _font, src, dst, fg);
			}
		}

		if (_enableCursor &&
			_currentFrameCycle < 30 &&
			_cursorRow >= 0 &&
			_cursorRow < rows &&
			_cursorColumn >= 0 &&
			_cursorColumn < columns)
		{
			// Draw an underline cursor (bottom 2 rows of the glyph cell)
			var cursorRect = new Rectangle(
				_cursorColumn * glyphWidth,
				((_cursorRow * glyphHeight) + glyphHeight) - 2,
				glyphWidth,
				2
			);
			Raylib.ImageDrawRectangleRec(ref _display, cursorRect, CgaPalette[7]); // light gray
		}
	}

	private void RenderMonochrome640x200Mode()
	{
		const int width = 640;
		const int height = 200;

		if (_display.Width != width || _display.Height != height)
		{
			Raylib.UnloadImage(_display);
			_display = Raylib.GenImageColor(width, height, Color.Black);
		}

		for (int y = 0; y < height; y++)
		{
			for (int i = 0; i < width / 8; i++)
			{
				uint address = (uint)((width / 8 * y) + i);
				byte pixels = (byte)_vram.Read(address, Constants.DataSize.Byte);

				for (int bit = 0; bit < 8; bit++)
				{
					bool lit = (pixels & (0x80 >> bit)) != 0;
					int x = (i * 8) + bit;
					Raylib.ImageDrawPixel(ref _display, x, y, lit ? CgaPalette[7] : CgaPalette[0]);
				}
			}
		}
	}

	private void Render4Color320x200Mode()
	{
		const int width = 320;
		const int height = 200;

		if (_display.Width != width || _display.Height != height)
		{
			Raylib.UnloadImage(_display);
			_display = Raylib.GenImageColor(width, height, Color.Black);
		}

		for (int y = 0; y < height; y++)
		{
			for (int byteIndex = 0; byteIndex < width / 4; byteIndex++)
			{
				uint addr = (uint)((width / 4 * y) + byteIndex);
				byte pixels = (byte)_vram.Read(addr, Constants.DataSize.Byte);

				for (int pair = 0; pair < 4; pair++)
				{
					int colorIndex = (pixels >> (6 - (pair * 2))) & 0x03;
					int x = (byteIndex * 4) + pair;
					Raylib.ImageDrawPixel(ref _display, x, y, CgaPalette[colorIndex]);
				}
			}
		}
	}
}
