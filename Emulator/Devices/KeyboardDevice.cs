using Raylib_cs;

namespace im8000emu.Emulator.Devices;

/// <summary>
///     Emulates a keyboard that matches PC-XT scancodes (set 1), but with a generic interface that will probably
///     be the Z80 PIO at some point.
///     Address map:
///     0 - STATUS (read)/CONFIG (write)
/// 	STATUS:
///     - b0: Data Ready
/// 	CONFIG:
///	- b0: Keyboard Disabled (0)/Enabled (1)
///     - b1: Interrupts Disabled (0)/Enabled (1)
///     1 - DATA (read)
/// </summary>
internal class KeyboardDevice : IMemoryDevice, IInterruptingDevice
{
	private const int MaxBufferCapacity = 32;
	private readonly Queue<byte> _buffer = [];
	private readonly HashSet<KeyboardKey> _currentlyPressedKeys = [];
	private bool _enableInterrupts = false;
	private bool _enableKeyboard = false;

	public bool RaisedInterrupt { get; private set; }
	public bool RaisedNonMaskableInterrupt => false;

	public byte OnInterruptAcknowledged()
	{
		// Probably need to figure out how we send signals with RETI/RETN to unlatch interrupt.
		RaisedInterrupt = false;
		return 0x08;
	}

	public uint Size => 4;

	public uint Read(uint address, Constants.DataSize size)
	{
		uint offset = address % Size;

		if (size != Constants.DataSize.Byte)
		{
			if (Config.EnableStrictMode)
			{
				throw new EmulatedMachineException($"Cannot access keyboard with {size} size!");
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
			0x00 => (uint)(_buffer.Count > 0 ? 1 : 0),
			0x01 => (uint)(_buffer.Count > 0 ? _buffer.Dequeue() : 0xFF),
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
				throw new EmulatedMachineException($"Cannot access keyboard with {size} size!");
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

		if (offset == 0)
		{
			_enableKeyboard = (value & 1) != 0;
			_enableInterrupts = (value & 2) != 0;
		}
		else if (Config.EnableStrictMode)
		{
			throw new EmulatedMachineException($"Cannot write to keyboard at {offset}!");
		}
	}

	// Called every frame. Could be ISteppingDevice, but don't need to be cycle accurate.
	public void Refresh()
	{
		if (!_enableKeyboard)
		{
			return;
		}

		var key = (KeyboardKey)Raylib.GetKeyPressed();

		while (key != KeyboardKey.Null)
		{
			_currentlyPressedKeys.Add(key);
			byte[] scancode = KeyToScancode(key);
			if (scancode.Length > 0)
			{
				AddKey(scancode);
			}

			key = (KeyboardKey)Raylib.GetKeyPressed();
		}

		List<KeyboardKey> keysToRemove = [];

		foreach (KeyboardKey pressedKey in _currentlyPressedKeys)
		{
			if (Raylib.IsKeyReleased(pressedKey))
			{
				keysToRemove.Add(pressedKey);
				byte[] scancode = KeyToScancode(pressedKey);
				if (scancode.Length > 0)
				{
					if (pressedKey == KeyboardKey.PrintScreen)
					{
						// Print screen is special
						scancode = [0xE0, 0xB7, 0xE0, 0xAA];
						AddKey(scancode);
					}
					else if (pressedKey != KeyboardKey.Pause)
					{
						scancode[^1] |= 0x80;
						AddKey(scancode);
					}
				}
			}
		}

		foreach (KeyboardKey pressedKey in keysToRemove)
		{
			_currentlyPressedKeys.Remove(pressedKey);
		}
	}

	private void AddKey(byte[] scancode)
	{
		if (_enableInterrupts)
		{
			RaisedInterrupt = true;
		}

		foreach (byte b in scancode)
		{
			if (_buffer.Count < MaxBufferCapacity)
			{
				_buffer.Enqueue(b);
			}
			// Else beep a speaker or something.
		}
	}

	private static byte[] KeyToScancode(KeyboardKey key)
	{
		return key switch
		{
			// Function keys
			KeyboardKey.F1 => [0x3B],
			KeyboardKey.F2 => [0x3C],
			KeyboardKey.F3 => [0x3D],
			KeyboardKey.F4 => [0x3E],
			KeyboardKey.F5 => [0x3F],
			KeyboardKey.F6 => [0x40],
			KeyboardKey.F7 => [0x41],
			KeyboardKey.F8 => [0x42],
			KeyboardKey.F9 => [0x43],
			KeyboardKey.F10 => [0x44],
			KeyboardKey.F11 => [0x57],
			KeyboardKey.F12 => [0x58],

			// Number row
			KeyboardKey.Grave => [0x29],
			KeyboardKey.One => [0x02],
			KeyboardKey.Two => [0x03],
			KeyboardKey.Three => [0x04],
			KeyboardKey.Four => [0x05],
			KeyboardKey.Five => [0x06],
			KeyboardKey.Six => [0x07],
			KeyboardKey.Seven => [0x08],
			KeyboardKey.Eight => [0x09],
			KeyboardKey.Nine => [0x0A],
			KeyboardKey.Zero => [0x0B],
			KeyboardKey.Minus => [0x0C],
			KeyboardKey.Equal => [0x0D],
			KeyboardKey.Backspace => [0x0E],

			// Top row
			KeyboardKey.Tab => [0x0F],
			KeyboardKey.Q => [0x10],
			KeyboardKey.W => [0x11],
			KeyboardKey.E => [0x12],
			KeyboardKey.R => [0x13],
			KeyboardKey.T => [0x14],
			KeyboardKey.Y => [0x15],
			KeyboardKey.U => [0x16],
			KeyboardKey.I => [0x17],
			KeyboardKey.O => [0x18],
			KeyboardKey.P => [0x19],
			KeyboardKey.LeftBracket => [0x1A],
			KeyboardKey.RightBracket => [0x1B],
			KeyboardKey.Backslash => [0x2B],

			// Home row
			KeyboardKey.CapsLock => [0x3A],
			KeyboardKey.A => [0x1E],
			KeyboardKey.S => [0x1F],
			KeyboardKey.D => [0x20],
			KeyboardKey.F => [0x21],
			KeyboardKey.G => [0x22],
			KeyboardKey.H => [0x23],
			KeyboardKey.J => [0x24],
			KeyboardKey.K => [0x25],
			KeyboardKey.L => [0x26],
			KeyboardKey.Semicolon => [0x27],
			KeyboardKey.Apostrophe => [0x28],
			KeyboardKey.Enter => [0x1C],

			// Bottom row
			KeyboardKey.LeftShift => [0x2A],
			KeyboardKey.Z => [0x2C],
			KeyboardKey.X => [0x2D],
			KeyboardKey.C => [0x2E],
			KeyboardKey.V => [0x2F],
			KeyboardKey.B => [0x30],
			KeyboardKey.N => [0x31],
			KeyboardKey.M => [0x32],
			KeyboardKey.Comma => [0x33],
			KeyboardKey.Period => [0x34],
			KeyboardKey.Slash => [0x35],
			KeyboardKey.RightShift => [0x36],

			// Modifiers / special
			KeyboardKey.LeftControl => [0x1D],
			KeyboardKey.LeftAlt => [0x38],
			KeyboardKey.Space => [0x39],
			KeyboardKey.RightAlt => [0xE0, 0x38],
			KeyboardKey.RightControl => [0xE0, 0x1D],

			// Navigation cluster
			KeyboardKey.Insert => [0xE0, 0x52],
			KeyboardKey.Home => [0xE0, 0x47],
			KeyboardKey.PageUp => [0xE0, 0x49],
			KeyboardKey.Delete => [0xE0, 0x53],
			KeyboardKey.End => [0xE0, 0x4F],
			KeyboardKey.PageDown => [0xE0, 0x51],

			// Arrow keys
			KeyboardKey.Up => [0xE0, 0x48],
			KeyboardKey.Left => [0xE0, 0x4B],
			KeyboardKey.Down => [0xE0, 0x50],
			KeyboardKey.Right => [0xE0, 0x4D],

			// Numpad
			KeyboardKey.NumLock => [0x45],
			KeyboardKey.KpDivide => [0xE0, 0x35],
			KeyboardKey.KpMultiply => [0x37],
			KeyboardKey.KpSubtract => [0x4A],
			KeyboardKey.KpAdd => [0x4E],
			KeyboardKey.KpEnter => [0xE0, 0x1C],
			KeyboardKey.KpDecimal => [0x53],
			KeyboardKey.Kp0 => [0x52],
			KeyboardKey.Kp1 => [0x4F],
			KeyboardKey.Kp2 => [0x50],
			KeyboardKey.Kp3 => [0x51],
			KeyboardKey.Kp4 => [0x4B],
			KeyboardKey.Kp5 => [0x4C],
			KeyboardKey.Kp6 => [0x4D],
			KeyboardKey.Kp7 => [0x47],
			KeyboardKey.Kp8 => [0x48],
			KeyboardKey.Kp9 => [0x49],

			// Misc
			KeyboardKey.Escape => [0x01],
			KeyboardKey.PrintScreen => [0xE0, 0x2A, 0xE0, 0x37], // Make only. Break sends alternate.
			KeyboardKey.ScrollLock => [0x46],
			KeyboardKey.Pause => [0xE1, 0x1D, 0x45, 0xE1, 0x9D, 0xC5], // No break code, long sequence

			_ => [],
		};
	}
}
