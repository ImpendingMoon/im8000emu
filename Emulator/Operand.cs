namespace im8000emu.Emulator
{
    internal class Operand
    {
        public Constants.RegisterTargets? Target { get; set; } // Register target, if applicable
        public uint? Immediate { get; set; } // Immediate value, if applicable
        public bool Indirect { get; set; } // Fetch operand indirectly from Target or Immediate?
        public short? Displacement { get; set; } // Signed offset for indirect access

        public override string ToString()
        {
            string str = string.Empty;

            if (Target.HasValue)
            {
                str = Target.Value.ToString();
            }
            else if (Immediate.HasValue)
            {
                str = $"0x{Immediate.Value:X}";
            }

            if (Indirect)
            {
                if (Displacement.HasValue)
                {
                    if (Displacement.Value < 0)
                    {
                        str = $"({str} - {-Displacement.Value})";
                    }
                    else
                    {
                        str = $"({str} + {Displacement.Value})";
                    }
                }
                else
                {
                    str = $"({str})";
                }
            }

            return str;
        }
    }
}
