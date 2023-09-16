namespace Mis6
{
    public class CpuFunctionConsole : CpuFunction
    {
        public override byte Get()
        {
            return System.ToByte((char)(byte)Console.Read());
        }

        public override void Set(byte value)
        {
            Console.Write(System.ToChar(value));
        }
    }

    public class CpuFunctionStackPointer : CpuFunction
    {
        private Cpu _cpu;
        private readonly bool _high;

        public CpuFunctionStackPointer(Cpu cpu, bool high)
        {
            _cpu = cpu;
            _high = high;
        }

        public override byte Get()
        {
            return _high ? System.GetHigh(_cpu.SP) : System.GetLow(_cpu.SP);
        }

        public override void Set(byte value)
        {
            if (_high)
            {
                _cpu.SP = ((_cpu.SP | (byte)(System.GetHigh(value) << 6)) & System.MaxWord);
            }
            else
            {
                _cpu.SP = (uint)(_cpu.SP | (uint)System.GetLow(value)) & System.MaxWord;
            }
        }
    }

    public class CpuFunctionStack : CpuFunction
    {
        private Cpu _cpu;
        private readonly bool _high;

        public CpuFunctionStack(Cpu cpu, bool wordMode)
        {
            _cpu = cpu;
            _high = wordMode;
        }

        public override byte Get()
        {
            return _cpu.Pop();
        }

        public override void Set(byte value)
        {
            _cpu.Push(value);
        }
    }

    public class CpuFunctionInstructionPointer : CpuFunction
    {
        private Cpu _cpu;
        private readonly bool _high;

        public CpuFunctionInstructionPointer(Cpu cpu, bool high)
        {
            _cpu = cpu;
            _high = high;
        }

        public override byte Get()
        {
            return _high ? System.GetHigh(_cpu.IP) : System.GetLow(_cpu.IP);
        }

        public override void Set(byte value)
        {
            if (_high)
            {
                _cpu.IP = ((_cpu.IP | (byte)(System.GetHigh(value) << 6)) & System.MaxWord);
            }
            else
            {
                _cpu.IP = (uint)(_cpu.IP | (uint)System.GetLow(value)) & System.MaxWord;
            }
        }
    }


    public class CpuFunctionZeroFlag : CpuFunction
    {
        private Cpu _cpu;

        public CpuFunctionZeroFlag(Cpu cpu)
        {
            _cpu = cpu;
        }

        public override byte Get()
        {
            return _cpu.ZF ? (byte)1 : (byte)0;
        }

        public override void Set(byte value)
        {
            _cpu.ZF = value > 0;
        }
    }


    public class CpuFunctionOverflowFlag : CpuFunction
    {
        private Cpu _cpu;

        public CpuFunctionOverflowFlag(Cpu cpu)
        {
            _cpu = cpu;
        }

        public override byte Get()
        {
            return _cpu.OF ? (byte)1 : (byte)0;
        }

        public override void Set(byte value)
        {
            _cpu.OF = value > 0;
        }
    }
}