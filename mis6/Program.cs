using System;
using System.Diagnostics;
using System.Net;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mis6
{
    public class Program
    {
        static void Main(string[] args)
        {
            var squasher = new Squasher();
            var annotatedFileName = args[0];
            var squashedFileName = Path.GetFileNameWithoutExtension(args[0]) + ".sq.txt";
            squasher.SquashFile(annotatedFileName, squashedFileName);
            var cpu = new Cpu();
            cpu.RomFile = squashedFileName;
            cpu.Start();
        }
    }

    public class System
    {
        public const uint BitsPerByte = 6;
        public const uint ByteRange = 1 << (6 + 1) - 1; // 64
        public const uint MaxByte = ByteRange - 1;
        public const uint WordRange = ByteRange * ByteRange;
        public const uint MaxWord = ByteRange * ByteRange - 1;

        public const string Charmap = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ .,!:;'\"+-*/\\=&|()[]<>{}@#$^";

        public static char ToChar(byte b)
        {
            return System.Charmap[b];
        }

        public static byte ToByte(char c)
        {
            return (byte)System.Charmap.IndexOf(c);
        }

        public static byte GetHigh(uint c)
        {
            return (byte)((c >> 6) & MaxByte);
        }

        public static byte GetLow(uint c)
        {
            return (byte)(c & MaxByte);
        }

        internal static uint ToWord(byte high, byte low)
        {
            return (uint)(high << 6 | low);
        }
    }

    public class Memory
    {
        byte[] _bytes = new byte[System.WordRange];
        private Cpu? _cpu;
        private readonly bool _writable;
        private bool _loading = false;

        public Memory(Cpu? cpu, bool writable)
        {
            _cpu = cpu;
            _writable = cpu != null && writable;
        }

        internal void LoadFromUtf8File(string fileName)
        {
            _loading = true;
            var image = File.ReadAllText(fileName);
            if (image.Length > System.WordRange)
            {
                throw new Exception("Out of memory");
            }

            for (uint i = 0; i < image.Length; i++)
            {
                SetByte(i, System.ToByte(image[(int)i]));
            }
            _loading = false;
        }

        internal uint GetWord(uint address)
        {
            return System.ToWord(GetByte(address), GetByte(address + 1));
        }

        internal void SetWord(uint address, uint value)
        {
            SetByte(address, System.GetHigh(value));
            SetByte(address + 1, System.GetLow(value));
        }

        internal byte GetByte(uint address)
        {
            address = address & System.MaxWord;
            if (_writable)
            {
                var function = _cpu!.Functions.GetValueOrDefault((int)address);
                if (function != null)
                {
                    return function.Get();
                }
            }

            return (byte)(_bytes[address] & System.MaxByte);
        }

        internal void SetByte(uint address, byte value)
        {
            if (!_writable && !_loading)
            {
                throw new Exception("This memory is not writable");
            }

            address = address & System.MaxWord;
            if (_cpu != null)
            {
                var function = _cpu!.Functions.GetValueOrDefault((int)address);
                if (function != null)
                {
                    function.Set((byte)(value & System.MaxByte));
                    return;
                }
            }

            _bytes[address] = (byte)(value & System.MaxByte);
        }

        internal bool SetAny(uint targetAddress, uint newValue, bool isRef, bool isWord)
        {
            if (isWord)
            {
                if (isRef)
                {
                    newValue = GetWord(newValue);
                }
                SetWord(targetAddress, newValue);
                return newValue == 0;
            }
            else
            {
                if (isRef)
                {
                    newValue = GetByte(newValue);
                }
                SetByte(targetAddress, (byte)newValue);
                return newValue == 0;
            }
        }

        internal uint GetAny(uint sourceAddress, bool isRef, bool isWord)
        {
            if (isWord)
            {
                if (!isRef)
                {
                    return sourceAddress & System.MaxWord;
                }

                var memValue = GetWord(sourceAddress);
                if (sourceAddress == 41)
                {
                    return System.ToWord(System.GetLow(memValue), System.GetHigh(memValue));
                }
                return memValue;
            }
            else
            {
                return !isRef ? sourceAddress & System.MaxByte : GetByte(sourceAddress);    
            }
        }

        internal uint GetAddress((ParameterType Type, uint Value, int Size) parameter)
        {
            if (parameter.Type == ParameterType.Constant)
            {
                return parameter.Value & System.MaxWord;
            }
            var address = parameter.Value;
            if (parameter.Type == ParameterType.PointerVariable)
            {
                address = GetWord(address);
            }
            return GetWord(address);
        }
    }

    public class Cpu
    {
        Memory _ram;
        Memory _code;
        public uint IP;
        byte A;
        public uint SP;

        public uint CodeStart { get; }


        public Dictionary<int, CpuFunction> Functions { get; }
        public string RomFile { get; set; }
        public bool ZF { get; set; }
        public bool OF { get; set; }

        private uint _register;
        private bool _wordMode;
        private Memory _paramMem;
        private bool _prevWasRegister = false;

        private bool _autoCarry;
        private char _parameterTypeChar;
        private ParameterType _parameterType;
        private bool _paramIsRef => _parameterType != ParameterType.Constant;  
        private uint _parameterValue;
        private bool _parameterRefRom = false;
        private byte _currentCodeByte;
        private uint _currentCodeWord;
        private const uint _stackSize = 128;
        private const uint _stackStart = System.MaxWord - _stackSize;
        private const uint _stackEnd = System.MaxWord;

        public Cpu()
        {
            Functions = new Dictionary<int, CpuFunction>();
            Functions.Add(0, new CpuFunctionDiscard());
            Functions.Add(5, new CpuFunctionConsole());
            Functions.Add(8, new CpuFunctionZeroFlag(this));
            Functions.Add(9, new CpuFunctionOverflowFlag(this));
            Functions.Add(40, new CpuFunctionStack(this, true));
            Functions.Add(41, new CpuFunctionStack(this, false));
            Functions.Add(52, new CpuFunctionInstructionPointer(this, true));
            Functions.Add(53, new CpuFunctionInstructionPointer(this, false));
            Functions.Add(54, new CpuFunctionStackPointer(this, true));
            Functions.Add(55, new CpuFunctionStackPointer(this, false));
        }

        public void Start()
        {
            Reset();
            Run();
        }

        public void Reset()
        {
            _ram = new Memory(this, true);
            IP = 0;
            A = 0;
            SP = _stackStart;
            ZF = false;
            OF = false;
            _prevWasRegister = false;
            _register = 0;
            _currentCodeByte = 0;
            _currentCodeWord = 0;
            _wordMode = false;
            _code = new Memory(null, false);
            _code.LoadFromUtf8File(RomFile);
        }

        public void Run()
        {
            while (true)
            {
                RunOne();
            }
        }

        public void RunOne()
        {
            char instruction = System.ToChar(FetchByte());
            if (instruction >= 'A' && instruction <= 'Z')
            {
                if (!_prevWasRegister)
                {
                    _register = System.ToByte(instruction);
                    _wordMode = false;
                    _prevWasRegister = true;
                }
                else
                {
                    if (_wordMode)
                    {
                        throw new Exception("More than 2 registers encountered in sequence.");
                    }
                    _wordMode = true;
                    var register2 = System.ToByte(instruction);
                    if (register2 != _register + 1)
                    {
                        throw new Exception("2 registers must go next to each other and come in sequence, for example AB or XY.");
                    }
                }
                return;
            }

            _prevWasRegister = false;
            _paramMem = _ram;
            switch (instruction)
            {
                case '>': // A>%xx  --- %xx = A
                    Store();
                    break;
                case '<': // A<%xx  --- A = %xx
                    Load();
                    break;
                case '&': // A&%xx  --- A = A & %xx
                    BitwiseAnd();
                    break;
                case '|': // A|%xx  --- A = A | %xx
                    BitwiseOr();
                    break;
                case '^': // A^%xx  --- A = A ^ %xx
                    BitwiseXor();
                    break;
                case '!': // A!     --- A = !A
                    BitwiseNot();
                    break;
                case '+': // A+%xx  --- A = A + %xx                   
                    Addition();
                    break;
                case '-': // A-%xx  --- A = A - %xx
                    Subtraction();
                    break;
                case '*': // A+%xx  --- A = A * %xx                   
                    Multiplication();
                    break;
                case '/': // A+%xx  --- A = A / %xx                   
                    Division();
                    break;
                case '\\': // A+%xx  --- A = A mod %xx                   
                    Modulo();
                    break;
                case '@': // @%xx  --- IP = %xx
                    FetchParameter(2);
                    IP = _paramMem.GetAny(_parameterValue, _paramIsRef, true);
                    break;
                case '=': // 0%xx  --- if (ZERO) IP = %xx
                    FetchParameter(2);
                    if (ZF)
                    {
                        IP = _paramMem.GetAny(_parameterValue, _paramIsRef, true);
                    }
                    break;
                case '#': // =%xx  --- STACK.PUSH(IP + 3); IP = %xx
                    FetchParameter(2);
                    var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, true);
                    _ram.SetAny(System.ToByte(':'), IP, false, true);
                    IP = memValue;
                    break;
                default:
                    throw new Exception("Instruction " + instruction + " not recognized.");
            }
        }

        private void Store()
        {
            FetchParameter(0);
            var registerValue = _paramMem.GetAny(_register, true, _wordMode);
            ZF = _ram.SetAny(_parameterValue, registerValue, false, _wordMode);
        }

        private void Load()
        {
            FetchParameter(_wordMode ? 2 : 1);
            var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, _wordMode);
            ZF = _ram.SetAny(_register, memValue, false, _wordMode);
        }

        private void BitwiseAnd()
        {
            FetchParameter(_wordMode ? 2 : 1);
            var registerValue = _ram.GetAny(_register, true, _wordMode);
            var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, _wordMode);
            var newValue = registerValue & memValue;
            ZF = _ram.SetAny(_register, newValue, false, _wordMode);
        }

        private void BitwiseOr()
        {
            FetchParameter(_wordMode ? 2 : 1);
            var registerValue = _ram.GetAny(_register, true, _wordMode);
            var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, _wordMode);
            var newValue = registerValue | memValue;
            ZF = _ram.SetAny(_register, newValue, false, _wordMode);
        }

        private void BitwiseXor()
        {
            FetchParameter(_wordMode ? 2 : 1);
            var registerValue = _ram.GetAny(_register, true, _wordMode);
            var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, _wordMode);
            var newValue = registerValue ^ memValue;
            ZF = _ram.SetAny(_register, newValue, false, _wordMode);
        }

        private void BitwiseNot()
        {
            var registerValue = _ram.GetAny(_register, true, _wordMode);
            var newValue = ~registerValue;
            ZF = _ram.SetAny(_register, newValue, false, _wordMode);
        }

        private void LowLevelAdd(byte a, byte b, ref byte c, out byte r)
        {
            byte a1, b1;
            r = 0;
            c = (byte)(c & 1);
            for (int i = 0; i < 6; i++)
            {
                a1 = (byte)((a >> i) & 1);
                b1 = (byte)((a >> i) & 1);
                r |= (byte)((a1 ^ b1 ^ c) << i);
                c = (byte)((a & b) | (b & c));
            }
        }

        private void Addition()
        {
            FetchParameter(_wordMode ? 2 : 1);
            var registerValue = _ram.GetAny(_register, true, _wordMode);
            var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, _wordMode);

            byte c = 0;
            byte r;
            
            if (!_wordMode)
            {
                LowLevelAdd((byte)registerValue, (byte)memValue, ref c, out r);
                _ram.SetByte(_register, r);
            }
            else
            {
                LowLevelAdd(System.GetLow(registerValue), System.GetLow(memValue), ref c, out r);
                ZF = r == 0;
                _ram.SetByte(_register + 1, r);
                LowLevelAdd(System.GetHigh(registerValue), System.GetLow(memValue), ref c, out r);
                ZF = ZF && r == 0;
                _ram.SetByte(_register, r);
            }

            OF = c == 1;
        }

        private void Multiplication()
        {
            FetchParameter(_wordMode ? 2 : 1);
            var registerValue = _ram.GetAny(_register, true, _wordMode);
            var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, _wordMode);
            var newValue = registerValue * memValue;
            OF = (newValue >> (_wordMode ? 12 : 6)) > 0;
            ZF = newValue == 0;
            _ram.SetAny(_register, newValue, false, _wordMode);
        }

        private void Division()
        {
            FetchParameter(_wordMode ? 2 : 1);
            var registerValue = _ram.GetAny(_register, true, _wordMode);
            var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, _wordMode);
            var newValue = registerValue / memValue;
            OF = false;
            ZF = newValue == 0;
            _ram.SetAny(_register, newValue, false, _wordMode);
        }

        private void Modulo()
        {
            FetchParameter(_wordMode ? 2 : 1);
            var registerValue = _ram.GetAny(_register, true, _wordMode);
            var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, _wordMode);
            var newValue = registerValue % memValue;
            OF = false;
            ZF = newValue == 0;
            _ram.SetAny(_register, newValue, false, _wordMode);
        }

        private void Subtraction()
        {
            FetchParameter(_wordMode ? 2 : 1);
            var registerValue = _ram.GetAny(_register, true, _wordMode);
            var memValue = _paramMem.GetAny(_parameterValue, _paramIsRef, _wordMode);
            var newValue = registerValue - memValue;
            OF = registerValue < memValue;
            ZF = newValue == 0;
            _ram.SetAny(_register, newValue, false, _wordMode);
        }

        public byte Pop()
        {
            var result = _ram.GetByte(SP--);

            if (SP < _stackStart)
            {
                throw new Exception("Stack underflow");
            }

            return result;
        }

        public void Push(byte v)
        {
            SP++;

            if (SP > _stackEnd)
            {
                throw new Exception("Stack overflow");
            }

            _ram.SetByte(SP, v);
        }

        public uint FetchWord()
        {
            var high = FetchByte();
            FetchByte();
            _currentCodeWord = System.ToWord(high, _currentCodeByte);
            return _currentCodeWord;
        }

        public byte FetchByte()
        {
            _currentCodeByte = _code.GetByte(IP++);
            return _currentCodeByte;
        }

        public void FetchParameter(int expectedConstantSize = 1, bool raw = false)
        {
            FetchByte();

            _parameterTypeChar = System.ToChar(_currentCodeByte);            

            if (_parameterTypeChar >= 'A' && _parameterTypeChar <= 'Z')
            {
                _parameterType = ParameterType.ValueVariable;
                _parameterValue = System.ToByte(_parameterTypeChar);
            }
            else if (_parameterTypeChar >= '0' && _parameterTypeChar <= '9')
            {
                if (expectedConstantSize == 0)
                {
                    throw new NotSupportedException("This instruction does not support constants.");
                }

                _parameterType = ParameterType.Constant;
                _parameterValue = System.ToByte(_parameterTypeChar);
            }
            else if (_parameterTypeChar == ':')
            {
                _parameterType = ParameterType.ValueVariable;
                _parameterValue = (byte)(System.ToByte(_parameterTypeChar));
            }
            else if (_parameterTypeChar == '\'' || _parameterTypeChar == '\"')
            {
                _parameterType = ParameterType.Constant;
                if (expectedConstantSize >= 1 && _parameterTypeChar == '\'')
                {
                    _parameterValue = FetchByte();
                }
                else if (expectedConstantSize >= 2 && _parameterTypeChar == '\"')
                {
                    _parameterValue = FetchWord();
                }
                else if (expectedConstantSize == 0)
                {
                    throw new NotSupportedException("This instruction does not support constants.");
                }
                else
                {
                    throw new NotSupportedException($"Parameter type {_parameterTypeChar} occured, but expected a {expectedConstantSize}-byte constant.");
                }
            }
            else if (_parameterTypeChar == '$' || _parameterTypeChar == '@')
            {
                _parameterType = ParameterType.ValueVariable;
                _parameterValue = FetchWord();
                if (_parameterTypeChar == '@')
                {
                    _paramMem = _code;
                }
            }
            else if (_parameterTypeChar == '*' || _parameterTypeChar == '&')
            {
                _parameterValue = FetchWord();
                if (raw)
                {
                    _parameterType = ParameterType.PointerVariable;
                }
                else
                {
                    _parameterType = ParameterType.ValueVariable;
                    _parameterValue = _ram.GetWord(_parameterValue);
                    if (_parameterTypeChar == '&')
                    {
                        _paramMem = _code;
                    }
                }
            }
            else
            {
                throw new NotImplementedException("Parameter type unknown: " + _parameterTypeChar);
            }
        }
    }
}

public enum ParameterType
{
    Constant,
    ValueVariable,
    PointerVariable,
}
