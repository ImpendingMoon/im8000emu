using System.Diagnostics;

namespace im8000emu.Emulator;

// Implements instruction decoding logic
internal partial class CPU
{
	/// <summary>
	///     Fetches and decodes the operation at the given address. Does not include interrupt servicing.
	/// </summary>
	/// <param name="address">Base address of the opcode</param>
	public DecodedOperation Decode(uint address)
	{
		_currentOperation.Reset();
		_currentOperation.BaseAddress = address;

		MemoryResult fetchResult = ReadMemory(address, Constants.OperandSize.Word);
		ushort instructionWord = (ushort)fetchResult.Value;
		_currentOperation.FetchCycles = fetchResult.Cycles;
		_currentOperation.OpcodeLength = 2;

		int group = instructionWord & 0b00000011;

		switch (group)
		{
			case 0b00:
			{
				DecodeRType(ref _currentOperation, instructionWord);
				break;
			}

			case 0b01:
			{
				DecodeRMType(ref _currentOperation, instructionWord);
				break;
			}

			case 0b10:
			{
				// Sub-Grouped Instructions
				int subgroup = (instructionWord >> 2) & 0b0000011;
				switch (subgroup)
				{
					case 0b00:
					{
						DecodeURType(ref _currentOperation, instructionWord);
						break;
					}
					case 0b01:
					{
						DecodeUMType(ref _currentOperation, instructionWord);
						break;
					}
					case 0b10:
					{
						DecodeBType(ref _currentOperation, instructionWord);
						break;
					}
					case 0b11:
					{
						DecodeNType(ref _currentOperation, instructionWord);
						break;
					}
					default:
					{
						Debug.Assert(false, "Unreachable code reached in opcode decoding.");
						break;
					}
				}

				break;
			}

			case 0b11:
			{
				// Special Instructions
				int subgroup = (instructionWord >> 2) & 0b0000011;
				switch (subgroup)
				{
					case 0b10:
					{
						DecodeBLType(ref _currentOperation, instructionWord);
						break;
					}
					case 0b11:
					{
						DecodeSBType(ref _currentOperation, instructionWord);
						break;
					}
					default:
					{
						// Other groups are reserved for expansion.
						throw new InvalidOperationException("Invalid special instruction subgroup.");
					}
				}
				break;
			}

			default:
			{
				Debug.Assert(false, "Unreachable code reached in opcode decoding.");
				break;
			}
		}

		return _currentOperation;
	}

	private void DecodeRType(ref DecodedOperation decodedOperation, ushort instructionWord)
	{
		/* Format R - Register-Register - (Group 00)
		 * Field Positions:
		 * - b0-b1 - Group (00)
		 * - b2-b7 - Opcode (6 bits)
		 * - b8-b9 - Size (2 bits)
		 * - b10-b12 - Destination Register (3 bits)
		 * - b13-b15 - Source Register (3 bits)
		 */

		// Decode operation
		byte operationSelector = (byte)((instructionWord >> 2) & 0b00111111);

		decodedOperation.Operation = operationSelector switch
		{
			0b000000 => Constants.Operation.LD,
			0b000001 => Constants.Operation.EX,
			0b000010 => Constants.Operation.ADD,
			0b000011 => Constants.Operation.ADC,
			0b000100 => Constants.Operation.SUB,
			0b000101 => Constants.Operation.SBC,
			0b000110 => Constants.Operation.CP,
			0b000111 => Constants.Operation.AND,
			0b001000 => Constants.Operation.OR,
			0b001001 => Constants.Operation.XOR,
			0b001010 => Constants.Operation.TST,
			0b001011 => Constants.Operation.BIT,
			0b001100 => Constants.Operation.SET,
			0b001101 => Constants.Operation.RES,
			0b001110 => Constants.Operation.RLC,
			0b001111 => Constants.Operation.RRC,
			0b010000 => Constants.Operation.RL,
			0b010001 => Constants.Operation.RR,
			0b010010 => Constants.Operation.SLA,
			0b010011 => Constants.Operation.SRA,
			0b010100 => Constants.Operation.SRL,
			_ => throw new InvalidOperationException(
				$"0b{operationSelector:B6} is not a valid R-Type operation selector"
			),
		};

		// Decode operand size
		byte sizeSelector = (byte)((instructionWord >> 8) & 0b00000011);
		decodedOperation.OperandSize = DecodeOperandSize(sizeSelector);

		// Decode destination
		byte operand1Selector = (byte)((instructionWord >> 10) & 0b00000111);

		if (operand1Selector == 0b111)
		{
			throw new InvalidOperationException("0b111 is not a valid destination target");
		}
		decodedOperation.Operand1 = new Operand
		{
			Target = DecodeRegisterTarget(operand1Selector, decodedOperation.OperandSize),
		};

		// Decode source
		byte operand2Selector = (byte)(instructionWord >> 13);
		if (operand2Selector == 0b111)
		{
			decodedOperation.Operand2 = new Operand
			{
				Immediate = FetchImmediate(ref decodedOperation, decodedOperation.OperandSize),
			};
		}
		else
		{
			decodedOperation.Operand2 = new Operand
			{
				Target = DecodeRegisterTarget(operand2Selector, decodedOperation.OperandSize),
			};
		}
	}

	private void DecodeRMType(ref DecodedOperation decodedOperation, ushort instructionWord)
	{
		/* Format RM - Register-Memory - (Group 01)
		 * Field Positions:
		 * - b0-b1 - Group (01)
		 * - b2-b6 - Opcode (5 bits)
		 * - b7 - Direction (1 bit)
		 * - b8-b9 - Size (2 bits)
		 * - b10-b12 - Register (3 bits)
		 * - b13-b15 - Address register (3 bits)
		 */

		// Decode operation
		byte operationSelector = (byte)((instructionWord >> 2) & 0b00011111);

		decodedOperation.Operation = operationSelector switch
		{
			0b00000 => Constants.Operation.LD,
			0b00001 => Constants.Operation.EX,
			0b00010 => Constants.Operation.ADD,
			0b00011 => Constants.Operation.ADC,
			0b00100 => Constants.Operation.SUB,
			0b00101 => Constants.Operation.SBC,
			0b00110 => Constants.Operation.CP,
			0b00111 => Constants.Operation.AND,
			0b01000 => Constants.Operation.OR,
			0b01001 => Constants.Operation.XOR,
			0b01010 => Constants.Operation.TST,
			0b01011 => Constants.Operation.BIT,
			0b01100 => Constants.Operation.SET,
			0b01101 => Constants.Operation.RES,
			0b01110 => Constants.Operation.RLC,
			0b01111 => Constants.Operation.RRC,
			0b10000 => Constants.Operation.RL,
			0b10001 => Constants.Operation.RR,
			0b10010 => Constants.Operation.SLA,
			0b10011 => Constants.Operation.SRA,
			0b10100 => Constants.Operation.SRL,
			0b10101 => Constants.Operation.IN_OUT,
			_ => throw new InvalidOperationException(
				$"0b{operationSelector:B6} is not a valid RM-Type operation selector"
			),
		};

		// Decode direction
		bool isLoad = ((instructionWord >> 7) & 0b1) == 1; // 0 = Reg to Mem, 1 = Mem to Reg

		// Decode operand size
		byte sizeSelector = (byte)((instructionWord >> 8) & 0b00000011);
		decodedOperation.OperandSize = DecodeOperandSize(sizeSelector);

		// Decode indirect (address) operand
		var indirectOperand = new Operand
		{
			Indirect = true,
		};

		byte indirectOperandSelector = (byte)(instructionWord >> 13);
		if (indirectOperandSelector == 0b111)
		{
			indirectOperand.Immediate = FetchImmediate(ref decodedOperation, Constants.OperandSize.DWord);
		}
		else
		{
			indirectOperand.Target = DecodeRegisterTarget(indirectOperandSelector, Constants.OperandSize.DWord);

			// IX, IY, SP always have a displacement. Displacements are always before immediate values.
			if (indirectOperand.Target >= Constants.RegisterTargets.IX)
			{
				indirectOperand.Displacement = (short)FetchImmediate(ref decodedOperation, Constants.OperandSize.Word);
			}
		}

		// Decode register operand
		var registerOperand = new Operand();

		byte registerOperandSelector = (byte)((instructionWord >> 10) & 0b00000111);

		if (registerOperandSelector == 0b111)
		{
			if (isLoad)
			{
				throw new InvalidOperationException(
					"0b111 is not a valid destination target for memory-to-register operations"
				);
			}
			if (indirectOperandSelector == 0b111)
			{
				throw new InvalidOperationException("0b111 is not a valid source target with direct addressing");
			}

			registerOperand.Immediate = FetchImmediate(ref decodedOperation, decodedOperation.OperandSize);
		}
		else
		{
			registerOperand.Target = DecodeRegisterTarget(registerOperandSelector, decodedOperation.OperandSize);
		}

		if (isLoad)
		{
			decodedOperation.Operand1 = registerOperand;
			decodedOperation.Operand2 = indirectOperand;
		}
		else
		{
			decodedOperation.Operand1 = indirectOperand;
			decodedOperation.Operand2 = registerOperand;
		}
	}

	private void DecodeURType(ref DecodedOperation decodedOperation, ushort instructionWord)
	{
		/* Format UR - Unary Register - (Group 10, Subgroup 00)
		 * Field Positions:
		 * - b0-b1 - Group (10)
		 * - b2-b3 - Sub-Group (00)
		 * - b4-b7 - Opcode (4 bits)
		 * - b8-b9 - Size (2 bits)
		 * - b10-b12 - Register (3 bits)
		 * - b13-b15 - Function (3 bits)
		 */

		// Operation selector is Opcode + Function. Easier in hardware than in software.
		byte operationSelector = (byte)((instructionWord >> 4) & 0b00001111);
		operationSelector = (byte)(operationSelector << 3);
		operationSelector |= (byte)((instructionWord >> 13) & 0b00000111);

		decodedOperation.Operation = operationSelector switch
		{
			0b0000000 => Constants.Operation.EX_Alt,
			0b0000001 => Constants.Operation.EXH,
			0b0000010 => Constants.Operation.PUSH,
			0b0000011 => Constants.Operation.POP,
			0b0000100 => Constants.Operation.INC,
			0b0000101 => Constants.Operation.DEC,
			0b0000110 => Constants.Operation.NEG,
			0b0000111 => Constants.Operation.EXT,
			0b0001000 => Constants.Operation.MLT,
			0b0001001 => Constants.Operation.DIV,
			0b0001010 => Constants.Operation.SDIV,
			0b0001011 => Constants.Operation.CPL,
			_ => throw new InvalidOperationException(
				$"0b{operationSelector:B7} is not a valid UR-Type operation selector"
			),
		};

		// Decode operand size
		byte sizeSelector = (byte)((instructionWord >> 8) & 0b00000011);
		decodedOperation.OperandSize = DecodeOperandSize(sizeSelector);

		// Decode target
		byte operand1Selector = (byte)((instructionWord >> 10) & 0b00000111);

		if (operand1Selector == 0b111)
		{
			throw new InvalidOperationException("0b111 is not a valid target selector for UR-type instructions");
		}
		decodedOperation.Operand1 = new Operand
		{
			Target = DecodeRegisterTarget(operand1Selector, decodedOperation.OperandSize),
		};
	}

	private void DecodeUMType(ref DecodedOperation decodedOperation, ushort instructionWord)
	{
		/* Format UM - Unary Memory - (Group 10, Subgroup 01)
		 * Field Positions:
		 * - b0-b1 - Group (10)
		 * - b2-b3 - Sub-Group (01)
		 * - b4-b7 - Opcode (4 bits)
		 * - b8-b9 - Size (2 bits)
		 * - b10-b12 - Function (3 bits)
		 * - b13-b15 - Address register (3 bits)
		 */

		// Operation selector is Opcode + Function. Easier in hardware than in software.
		byte operationSelector = (byte)((instructionWord >> 4) & 0b00001111);
		operationSelector = (byte)(operationSelector << 3);
		operationSelector |= (byte)((instructionWord >> 13) & 0b00000111);

		decodedOperation.Operation = operationSelector switch
		{
			0b0000001 => Constants.Operation.EXH,
			0b0000010 => Constants.Operation.PUSH,
			0b0000100 => Constants.Operation.INC,
			0b0000101 => Constants.Operation.DEC,
			0b0000110 => Constants.Operation.NEG,
			0b0000111 => Constants.Operation.EXT,
			0b0001000 => Constants.Operation.MLT,
			0b0001001 => Constants.Operation.DIV,
			0b0001010 => Constants.Operation.SDIV,
			0b0001011 => Constants.Operation.CPL,
			_ => throw new InvalidOperationException(
				$"0b{operationSelector:B7} is not a valid UM-Type operation selector"
			),
		};

		// Decode operand size
		byte sizeSelector = (byte)((instructionWord >> 8) & 0b00000011);
		decodedOperation.OperandSize = DecodeOperandSize(sizeSelector);

		// Decode target (indirect)
		var operand1 = new Operand
		{
			Indirect = true,
		};

		byte operand1Selector = (byte)(instructionWord >> 13);
		if (operand1Selector == 0b111)
		{
			operand1.Immediate = FetchImmediate(ref decodedOperation, Constants.OperandSize.DWord);
		}
		else
		{
			operand1.Target = DecodeRegisterTarget(operand1Selector, Constants.OperandSize.DWord);
			// IX, IY, SP always have a displacement.
			if (operand1.Target >= Constants.RegisterTargets.IX)
			{
				operand1.Displacement = (short)FetchImmediate(ref decodedOperation, Constants.OperandSize.Word);
			}
		}

		decodedOperation.Operand1 = operand1;
	}

	private void DecodeBType(ref DecodedOperation decodedOperation, ushort instructionWord)
	{
		/* Format B - Branch - (Group 10-10)
		 *
		 * Field Positions:
		 * - b0-b1 - Group (10)
		 * - b2-b3 - Sub-Group (10)
		 * - b4-b8 - Opcode (5 bits)
		 * - b9-b12 - Condition (4 bits)
		 * - b13-b15 - Address register (3 bits)
		 */

		// Decode operation
		byte operationSelector = (byte)((instructionWord >> 4) & 0b00011111);
		decodedOperation.Operation = operationSelector switch
		{
			0b00000 => Constants.Operation.JP,
			0b00001 => Constants.Operation.JR_s8,
			0b00010 => Constants.Operation.JR,
			0b00100 => Constants.Operation.CALL,
			0b00101 => Constants.Operation.CALLR_s8,
			0b00110 => Constants.Operation.CALLR,
			0b01000 => Constants.Operation.RET,
			0b01001 => Constants.Operation.RETI,
			0b01010 => Constants.Operation.RETN,
			_ => throw new InvalidOperationException(
				$"0b{operationSelector:B5} is not a valid B-Type operation selector"
			),
		};

		// Decode condition
		byte conditionSelector = (byte)((instructionWord >> 9) & 0b00001111);
		decodedOperation.Condition = DecodeCondition(conditionSelector);

		// Size implied by operation
		decodedOperation.OperandSize = decodedOperation.Operation switch
		{
			Constants.Operation.JR_s8 or Constants.Operation.CALLR_s8 => Constants.OperandSize.Byte,
			Constants.Operation.JR or Constants.Operation.CALLR => Constants.OperandSize.Word,
			_ => Constants.OperandSize.DWord,
		};

		// Decode target
		byte operand1Selector = (byte)(instructionWord >> 13);
		if (operand1Selector == 0b111)
		{
			decodedOperation.Operand1 = new Operand
			{
				Immediate = FetchImmediate(ref decodedOperation, decodedOperation.OperandSize),
			};
		}
		else
		{
			decodedOperation.Operand1 = new Operand
			{
				Target = DecodeRegisterTarget(operand1Selector, decodedOperation.OperandSize),
			};
		}
	}

	private void DecodeNType(ref DecodedOperation decodedOperation, ushort instructionWord)
	{
		/* Format N - Nullary - (Group 10, Subgroup 11)
		 * Field Positions:
		 * - b0-b1 - Group (10)
		 * - b2-b3 - Sub-Group (11)
		 * - b4-b7 - Opcode (4 bits)
		 * - b8-b15 - Function (8 bits)
		 */

		// Decode operation
		byte operationSelector = (byte)((instructionWord >> 4) & 0b00001111);
		byte functionSelector = (byte)(instructionWord >> 8);

		// Opcode acts as grouping for functions
		switch (operationSelector)
		{
			// Exchange
			case 0b0100:
			{
				decodedOperation.Operation = functionSelector switch
				{
					0b00000000 => Constants.Operation.EXX,
					0b00000001 => Constants.Operation.EXI,
					_ => throw new InvalidOperationException(
						$"0b{functionSelector:B8} is not a valid Exchange function selector"
					),
				};
				break;
			}
			// Control Flow
			case 0b0101:
			{
				decodedOperation.Operation = functionSelector switch
				{
					0b00000000 => Constants.Operation.RST,
					0b00000001 => Constants.Operation.SCF,
					0b00000010 => Constants.Operation.CCF,
					_ => throw new InvalidOperationException(
						$"0b{functionSelector:B8} is not a valid Control Flow function selector"
					),
				};

				// RST takes an immediate byte operand
				if (decodedOperation.Operation == Constants.Operation.RST)
				{
					decodedOperation.OperandSize = Constants.OperandSize.Byte;
					decodedOperation.Operand1 = new Operand
					{
						Immediate = FetchImmediate(ref decodedOperation, Constants.OperandSize.Byte),
					};
				}
				break;
			}
			// BCD
			case 0b0110:
			{
				decodedOperation.Operation = functionSelector switch
				{
					0b00000000 => Constants.Operation.DAA,
					0b00000001 => Constants.Operation.RLD,
					0b00000010 => Constants.Operation.RRD,
					_ => throw new InvalidOperationException(
						$"0b{functionSelector:B8} is not a valid BCD function selector"
					),
				};
				break;
			}
			// System
			case 0b1000:
			{
				decodedOperation.Operation = functionSelector switch
				{
					0b00000000 => Constants.Operation.HALT,
					0b00000001 => Constants.Operation.EI,
					0b00000010 => Constants.Operation.DI,
					0b00000011 => Constants.Operation.IM1,
					0b00000100 => Constants.Operation.IM2,
					0b00000101 => Constants.Operation.LD_I_NN,
					0b00000110 => Constants.Operation.LD_R_A,
					0b00000111 => Constants.Operation.LD_A_R,
					_ => throw new InvalidOperationException(
						$"0b{functionSelector:B8} is not a valid System function selector"
					),
				};

				// LD_I_NN takes an immediate dword operand, LD_R_A and LD_A_R operate on implied registers
				if (decodedOperation.Operation == Constants.Operation.LD_I_NN)
				{
					decodedOperation.OperandSize = Constants.OperandSize.DWord;
					decodedOperation.Operand1 = new Operand
					{
						Immediate = FetchImmediate(ref decodedOperation, Constants.OperandSize.DWord),
					};
				}
				break;
			}
			default:
			{
				throw new InvalidOperationException(
					$"0b{operationSelector:B4} is not a valid N-Type operation selector"
				);
			}
		}
	}

	private void DecodeBLType(ref DecodedOperation decodedOperation, ushort instructionWord)
	{
		/* Format BL - Block (Group 11, Subgroup 10)
		 * Field Positions:
		 *  - b0-b1 - Group (11)
		 *  - b2-b3 - Group (10)
		 *  - b4-b7 - Opcode
		 *  - b8-b9 - Size
		 *  - b10 - Increment/Decrement (0 = Decrement, 1 = Increment)
		 *  - b11 - Repeat (0 = Once, 1 = Repeat)
		 *  - b12-b15 - Function
		 */

		// This field was added on after starting the emulator, so all the block instructions are implemented individually.
		// If we add more block ops I'll structure this better.
		decodedOperation.Operation = instructionWord switch
		{
			0x050B => Constants.Operation.LDI,
			0x010B => Constants.Operation.LDD,
			0x0D0B => Constants.Operation.LDIR,
			0x090B => Constants.Operation.LDDR,
			0x051B => Constants.Operation.CPI,
			0x011B => Constants.Operation.CPD,
			0x0D1B => Constants.Operation.CPIR,
			0x091B => Constants.Operation.CPDR,
			0x151B => Constants.Operation.TSI,
			0x111B => Constants.Operation.TSD,
			0x1D1B => Constants.Operation.TSIR,
			0x191B => Constants.Operation.TSDR,
			0x052B => Constants.Operation.INI,
			0x012B => Constants.Operation.IND,
			0x0D2B => Constants.Operation.INIR,
			0x092B => Constants.Operation.INDR,
			0x053B => Constants.Operation.OUTI,
			0x013B => Constants.Operation.OUTD,
			0x0D3B => Constants.Operation.OTIR,
			0x093B => Constants.Operation.OTDR,
			_ => throw new InvalidOperationException($"0b{instructionWord:B16} is not a valid Block operation"),
		};
	}

	private void DecodeSBType(ref DecodedOperation decodedOperation, ushort instructionWord)
	{
		/* Format SB - Single-Byte - (Group 11, Subgroup 11)
		 * Field Positions:
		 * - b0-b1 - Group (11)
		 * - b2-b3 - Sub-Group (11)
		 * - b4-b7 - Opcode (4 bits)
		 */

		byte opcodeByte = (byte)instructionWord; // low byte
		byte highByte = (byte)(instructionWord >> 8); // immediate or unused

		byte operationSelector = (byte)(opcodeByte >> 4);

		decodedOperation.Operation = operationSelector switch
		{
			0b0000 => Constants.Operation.NOP,
			0b0001 => Constants.Operation.DJNZ,
			0b0010 => Constants.Operation.JANZ,
			_ => throw new InvalidOperationException(
				$"0b{operationSelector:B4} is not a valid single-byte operation selector"
			),
		};

		decodedOperation.OperandSize = Constants.OperandSize.Byte;

		if (decodedOperation.Operation == Constants.Operation.NOP)
		{
			// The high byte of the instruction word is discarded, and will be refetched
			decodedOperation.OpcodeLength = 1;
		}
		else
		{
			// DJNZ / JANZ: the signed offset is packed into the high byte of the 16-bit fetch.
			decodedOperation.Operand1 = new Operand
			{
				Immediate = highByte,
			};
		}
	}

	private static Constants.OperandSize DecodeOperandSize(byte selector)
	{
		return selector switch
		{
			0b00 => Constants.OperandSize.Byte,
			0b01 => Constants.OperandSize.Word,
			0b10 => Constants.OperandSize.DWord,
			_ => throw new ArgumentException($"0b{selector:B} is not a valid operand size selector"),
		};
	}

	private static Constants.RegisterTargets DecodeRegisterTarget(byte selector, Constants.OperandSize size)
	{
		if (size is Constants.OperandSize.Byte or Constants.OperandSize.Word)
		{
			return selector switch
			{
				0b000 => Constants.RegisterTargets.A,
				0b001 => Constants.RegisterTargets.B,
				0b010 => Constants.RegisterTargets.C,
				0b011 => Constants.RegisterTargets.D,
				0b100 => Constants.RegisterTargets.E,
				0b101 => Constants.RegisterTargets.H,
				0b110 => Constants.RegisterTargets.L,
				_ => throw new ArgumentException($"0b{selector:B} is not a valid register selector"),
			};
		}

		return selector switch
		{
			0b000 => Constants.RegisterTargets.AF,
			0b001 => Constants.RegisterTargets.BC,
			0b010 => Constants.RegisterTargets.DE,
			0b011 => Constants.RegisterTargets.HL,
			0b100 => Constants.RegisterTargets.IX,
			0b101 => Constants.RegisterTargets.IY,
			0b110 => Constants.RegisterTargets.SP,
			_ => throw new ArgumentException($"{selector} is not a valid register selector"),
		};
	}

	private static Constants.Condition DecodeCondition(byte selector)
	{
		return selector switch
		{
			0b0000 => Constants.Condition.NZ,
			0b0001 => Constants.Condition.Z,
			0b0010 => Constants.Condition.NC,
			0b0011 => Constants.Condition.C,
			0b0100 => Constants.Condition.PO,
			0b0101 => Constants.Condition.PE,
			0b0110 => Constants.Condition.P,
			0b0111 => Constants.Condition.N,
			0b1111 => Constants.Condition.Unconditional,
			_ => throw new ArgumentException($"0b{selector:B4} is not a valid condition selector"),
		};
	}
}
