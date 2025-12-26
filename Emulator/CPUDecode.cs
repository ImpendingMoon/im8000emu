using System.Diagnostics;

namespace im8000emu.Emulator
{
    internal partial class CPU
    {
        /// <summary>
        /// Fetches and decodes the operation at the given address. Does not include interrupt servicing.
        /// </summary>
        /// <param name="address">Base address of the opcode</param>
        public DecodedOperation Decode(uint address)
        {
            // Placeholder
            var decodedOperation = new DecodedOperation
            {
                BaseAddress = address,
                Opcode = [_memoryBus.ReadByte(address)]
            };

            var group = decodedOperation.Opcode[0] & 0b00000011;

            // TODO: Implement methods to decode each instruction group.
            switch (group)
            {
                case 0b00:
                {
                    DecodeRType(decodedOperation);
                    break;
                }

                case 0b01:
                {
                    DecodeRMType(decodedOperation);
                    break;
                }

                case 0b10:
                {
                    // Sub-Grouped Instructions
                    var subgroup = (decodedOperation.Opcode[0] >> 2) & 0b0000011;
                    switch (subgroup)
                    {
                        case 0b00:
                        {
                            // Unary Register Instructions
                            break;
                        }
                        case 0b01:
                        {
                            // Unary Memory Instructions
                            break;
                        }
                        case 0b10:
                        {
                            // Branch Instructions
                            break;
                        }
                        case 0b11:
                        {
                            // Nullary Instructions
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
                    // Variable Length Instructions
                    var subgroup = (decodedOperation.Opcode[0] >> 2) & 0b0000011;
                    switch (subgroup)
                    {
                        case 0b11:
                        {
                            // Single byte Instructions
                            break;
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

            return decodedOperation;
        }

        private void DecodeRType(DecodedOperation decodedOperation)
        {
            /* Format R - Register-Register - (Group 00)
             * Field Positions:
             * - b0-b1 - Group (00)
             * - b2-b7 - Opcode (6 bits)
             * - b8-b9 - Size (2 bits)
             * - b10-b12 - Destination Register (3 bits)
             * - b13-b15 - Source Register (3 bits)
             */

            // Fetch the second byte of the instruction word
            decodedOperation.Opcode.Add(_memoryBus.ReadByte(decodedOperation.BaseAddress + 1));
            ushort instructionWord = BitConverter.ToUInt16(decodedOperation.Opcode.ToArray(), 0);

            // Decode operation
            byte operationSelector = (byte)((instructionWord >> 2) & 0b00111111);

            decodedOperation.Operation = operationSelector switch
            {
                0b000000 => Constants.Operation.LD,
                0b000001 => Constants.Operation.EX,
                0b000010 => Constants.Operation.IN,
                0b000011 => Constants.Operation.OUT,
                0b000100 => Constants.Operation.ADD,
                0b000101 => Constants.Operation.ADC,
                0b000110 => Constants.Operation.SUB,
                0b000111 => Constants.Operation.SBC,
                0b001000 => Constants.Operation.CP,
                0b001001 => Constants.Operation.AND,
                0b001010 => Constants.Operation.OR,
                0b001011 => Constants.Operation.XOR,
                0b001100 => Constants.Operation.TST,
                0b001101 => Constants.Operation.BIT,
                0b001110 => Constants.Operation.SET,
                0b001111 => Constants.Operation.RES,
                0b010000 => Constants.Operation.RLC,
                0b010001 => Constants.Operation.RRC,
                0b010010 => Constants.Operation.RL,
                0b010011 => Constants.Operation.RR,
                0b010100 => Constants.Operation.SLA,
                0b010101 => Constants.Operation.SRA,
                0b010110 => Constants.Operation.SRL,
                _ => throw new InvalidOperationException($"0b{operationSelector:B6} is not a valid R-Type operation selector"),
            };

            // Decode operand size
            byte sizeSelector = (byte)((instructionWord >> 8) & 0b00000011);
            decodedOperation.OperandSize = DecodeOperandSize(sizeSelector);

            // Decode targets
            decodedOperation.Operand1 = new Operand
            {
                Target = null,
                Immediate = null,
                Indirect = false,
                Displacement = null
            };

            byte operand1Selector = (byte)((instructionWord >> 10) & 0b00000111);

            if (operand1Selector == 0b111)
            {
                throw new InvalidOperationException("0b111 is not a valid destination target");
            }
            else
            {
                decodedOperation.Operand1.Target = DecodeRegisterTarget(operand1Selector, decodedOperation.OperandSize);
            }

            decodedOperation.Operand2 = new Operand
            {
                Target = null,
                Immediate = null,
                Indirect = false,
                Displacement = null
            };

            byte operand2Selector = (byte)(instructionWord >> 13);
            if (operand2Selector == 0b111)
            {
                uint immediateValue = ReadImmediateValue(decodedOperation.BaseAddress + 2, decodedOperation.OperandSize);
                decodedOperation.Operand2.Immediate = immediateValue;
                AddImmediateValueToOpcode(decodedOperation.Opcode, decodedOperation.OperandSize, immediateValue);
            }
            else
            {
                decodedOperation.Operand2.Target = DecodeRegisterTarget(operand2Selector, decodedOperation.OperandSize);
            }

            decodedOperation.DisplayString = $"{decodedOperation.Operation} {decodedOperation.Operand1}, {decodedOperation.Operand2}";
        }

        private void DecodeRMType(DecodedOperation decodedOperation)
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

            // Fetch the second byte of the instruction word
            decodedOperation.Opcode.Add(_memoryBus.ReadByte(decodedOperation.BaseAddress + 1));
            ushort instructionWord = BitConverter.ToUInt16(decodedOperation.Opcode.ToArray(), 0);

            // Decode operation
            byte operationSelector = (byte)((instructionWord >> 2) & 0b00011111);

            decodedOperation.Operation = operationSelector switch
            {
                0b00000 => Constants.Operation.LD,
                0b00001 => Constants.Operation.EX,
                0b00010 => Constants.Operation.IN,
                0b00011 => Constants.Operation.OUT,
                0b00100 => Constants.Operation.ADD,
                0b00101 => Constants.Operation.ADC,
                0b00110 => Constants.Operation.SUB,
                0b00111 => Constants.Operation.SBC,
                0b01000 => Constants.Operation.CP,
                0b01001 => Constants.Operation.AND,
                0b01010 => Constants.Operation.OR,
                0b01011 => Constants.Operation.XOR,
                0b01100 => Constants.Operation.TST,
                0b01101 => Constants.Operation.BIT,
                0b01110 => Constants.Operation.SET,
                0b01111 => Constants.Operation.RES,
                0b10000 => Constants.Operation.RLC,
                0b10001 => Constants.Operation.RRC,
                0b10010 => Constants.Operation.RL,
                0b10011 => Constants.Operation.RR,
                0b10100 => Constants.Operation.SLA,
                0b10101 => Constants.Operation.SRA,
                0b10110 => Constants.Operation.SRL,
                _ => throw new InvalidOperationException($"0b{operationSelector:B6} is not a valid RM-Type operation selector"),
            };

            // Decode direction
            bool isLoad = ((instructionWord >> 7) & 0b1) == 1; // 0 = Reg to Mem, 1 = Mem to Reg

            // Decode operand size
            byte sizeSelector = (byte)((instructionWord >> 8) & 0b00000011);
            decodedOperation.OperandSize = DecodeOperandSize(sizeSelector);

            // Decode targets
            var indirectOperand = new Operand
            {
                Target = null,
                Immediate = null,
                Indirect = true,
                Displacement = null
            };

            byte indirectOperandSelector = (byte)(instructionWord >> 13);
            if (indirectOperandSelector == 0b111)
            {
                uint immediateValue = ReadImmediateValue(decodedOperation.BaseAddress + 2, decodedOperation.OperandSize);
                indirectOperand.Immediate = immediateValue;
                AddImmediateValueToOpcode(decodedOperation.Opcode, decodedOperation.OperandSize, immediateValue);
            }
            else
            {
                indirectOperand.Target = DecodeRegisterTarget(indirectOperandSelector, decodedOperation.OperandSize);

                // IX, IY, SP always have a displacement. Displacements are always before immediate values.
                if (indirectOperand.Target >= Constants.RegisterTargets.IX)
                {
                    short displacement = (short)ReadMemoryWord(decodedOperation.BaseAddress);
                    indirectOperand.Displacement = displacement;
                    AddDisplacementToOpcode(decodedOperation.Opcode, displacement);
                }
            }

            var registerOperand = new Operand
            {
                Target = null,
                Immediate = null,
                Indirect = false,
                Displacement = null
            };

            // ISA DESIGN NOTE:
            // Maybe it's better to swap Address and Register operands so that we decode in the order they appear in the instruction word?
            // Address operand is decoded first because it may have a displacement, which is always before immediate values in the instruction stream.
            // But we may also forbid immediate values for register operands, which would simplify the decoding logic, especially on hardware.
            // We'll see how often immediate values are used in practice before making that change.
            byte registerOperandSelector = (byte)((instructionWord >> 10) & 0b00000111);

            if (registerOperandSelector == 0b111)
            {
                if (isLoad)
                {
                    throw new InvalidOperationException("0b111 is not a valid destination target for memory-to-register operations");
                }

                uint immediateValue = ReadImmediateValue(decodedOperation.BaseAddress + (uint)decodedOperation.Opcode.Count, decodedOperation.OperandSize);
                registerOperand.Immediate = immediateValue;
                AddImmediateValueToOpcode(decodedOperation.Opcode, decodedOperation.OperandSize, immediateValue);
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

            decodedOperation.DisplayString = $"{decodedOperation.Operation} {decodedOperation.Operand1}, {decodedOperation.Operand2}";
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
            if (size == Constants.OperandSize.Byte || size == Constants.OperandSize.Word)
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

        private uint ReadImmediateValue(uint address, Constants.OperandSize size)
        {
            return size switch
            {
                Constants.OperandSize.Byte => ReadMemoryByte(address),
                Constants.OperandSize.Word => ReadMemoryWord(address),
                Constants.OperandSize.DWord => ReadMemoryDWord(address),
                _ => throw new ArgumentException($"{size} is not a valid operand size"),
            };
        }

        private void AddImmediateValueToOpcode(List<byte> opcode, Constants.OperandSize size, uint immediateValue)
        {
            byte[] immediateBytes = BitConverter.GetBytes(immediateValue);

            switch (size)
            {
                case Constants.OperandSize.Byte:
                {
                    opcode.Add(immediateBytes[0]);
                    break;
                }

                case Constants.OperandSize.Word:
                {
                    opcode.AddRange(immediateBytes[0..1]);
                    break;
                }

                case Constants.OperandSize.DWord:
                {
                    opcode.AddRange(immediateBytes);
                    break;
                }

                default: throw new ArgumentException($"{size} is not a valid operand size");
            }
        }

        private void AddDisplacementToOpcode(List<byte> opcode, short displacement)
        {
            byte[] displacementBytes = BitConverter.GetBytes(displacement);
            opcode.AddRange(displacementBytes);
        }
    }
}
