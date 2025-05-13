#define DEBUG_TRACE_EXECUTION

using System;

namespace LingV;

public enum InterpretResult
{
    INTERPRET_OK,
    INTERPRET_COMPILE_ERROR,
    INTERPRET_RUNTIME_ERROR
}

public enum SpecialRegisters
{
    RAX = 0
}

public class VirtualMachine
{
    private int PC = 0;
    private Value AX = Value.NilVal();
    private Chunk _chunk;
    private readonly List<Value> _registers = [];
    private readonly Stack<Value> _stack = [];
    private readonly Dictionary<string, Value> _globals = [];
    private bool _hadRTE = false;

    public InterpretResult Interpret(string source, int gpRegNum)
    {
        Chunk chunk = new();
        Compiler compiler = new(gpRegNum);

        if (!compiler.Compile(source, chunk))
            return InterpretResult.INTERPRET_COMPILE_ERROR;

        _chunk = chunk;
        PC = 0;

        for (int i = 0; i < 1 + gpRegNum; ++i)
        {
            _registers.Add(Value.NilVal());
        }

        //return InterpretResult.INTERPRET_OK;
        return Run();
    }

    private InterpretResult Run()
    {
        for (; ;)
        {
#if DEBUG_TRACE_EXECUTION
            Value[] slots = [.. _stack];
            Console.Write("          ");
            for (int s = 0; s < slots.Length; ++s)
            {
                Console.Write($"[ {slots[s].ToString():G} ]");
            }
            Console.WriteLine();
            Debug.DisassembleInstruction(_chunk, PC);
#endif
            byte instruction = ReadByte();
            ushort offset;
            int reg, mem;

            switch (instruction)
            {
                case (byte)OpCode.OP_CONSTANT:
                    Value constant = ReadConstant();
                    _stack.Push(constant);
                    break;
                case (byte)OpCode.OP_REGISTER:
                    reg = ReadInt();

                    Console.WriteLine($"Reg: {reg} has value Value: {_registers[reg]}");
                    break;
                case (byte)OpCode.OP_NIL:
                    _stack.Push(Value.NilVal());
                    break;
                case (byte)OpCode.OP_TRUE:
                    _stack.Push(Value.BoolVal(true));
                    break;
                case (byte)OpCode.OP_FALSE:
                    _stack.Push(Value.BoolVal(false));
                    break;
                case (byte)OpCode.OP_PUSH:
                    reg = ReadInt();
                    _stack.Push(_registers[reg]);
                    break;
                case (byte)OpCode.OP_POP:
                    _stack.Pop();
                    break;
                case (byte)OpCode.OP_POP_STORE:
                    reg = ReadInt();
                    _registers[reg] = _stack.Pop();
                    break;
                case (byte)OpCode.OP_MOV:
                    reg = ReadInt();
                    mem = ReadInt();

                    _registers[reg] = mem >= 0 ? _registers[mem] : _chunk.ReadConstant(mem);
                    break;
                case (byte)OpCode.OP_EQUAL:
                    BinaryOp('=');
                    break;
                case (byte)OpCode.OP_GREATER:
                    BinaryOp('>');
                    break;
                case (byte)OpCode.OP_LESS:
                    BinaryOp('<');
                    break;
                case (byte)OpCode.OP_ADD:
                    BinaryOp('+');
                    break;
                case (byte)OpCode.OP_SUBTRACT:
                    BinaryOp('-');
                    break;
                case (byte)OpCode.OP_MULTIPLY:
                    BinaryOp('*');
                    break;
                case (byte)OpCode.OP_DIVIDE:
                    BinaryOp('/');
                    break;
                case (byte)OpCode.OP_NOT:
                    reg = ReadInt();

                    _registers[reg] = Value.BoolVal(IsFalsey(_registers[reg]));
                    break;
                case (byte)OpCode.OP_NEGATE:
                    reg = ReadRegister();

                    if (!_registers[reg].IsNumber())
                    {
                        RuntimeError("Operand must be a number.");
                        return InterpretResult.INTERPRET_RUNTIME_ERROR;
                    }

                    _registers[reg] = Value.NumberVal(-_registers[reg].number);
                    break;
                case (byte)OpCode.OP_PRINT:
                    reg = ReadInt();

                    if (reg >= 0)
                        _registers[reg].PrintValue();
                    else
                        _chunk.ReadConstant(reg).PrintValue();
                    
                    break;
                case (byte)OpCode.OP_JUMP:
                    offset = ReadJumpShort();

                    PC += offset;
                    break;
                case (byte)OpCode.OP_JUMP_IF_FALSE:
                    offset = ReadJumpShort();

                    if (IsFalsey(Peek(0)))
                        PC += offset;
                    break;
                case (byte)OpCode.OP_LOOP:
                    offset = ReadJumpShort();
                    PC -= offset;
                    break;
                case (byte)OpCode.OP_RETURN:
                    Console.WriteLine($"{_registers[0]}");
                    Console.WriteLine($"{_registers[1]}");
                    return InterpretResult.INTERPRET_OK;
            }

            if (_hadRTE)
                return InterpretResult.INTERPRET_RUNTIME_ERROR;
        }
    }

    private Value Peek(int distance)
    {
        Value[] vals = [.. _stack];
        distance = (vals.Length - 1) - distance;
        return vals[distance];
    }

    private void InsertInStack(int slot, Value value)
    {
        Stack<Value> tempStack = [];

        for (int i = 0; i < _stack.Count - slot; ++i)
        {
            tempStack.Push(_stack.Pop());
        }

        tempStack.Pop();
        _stack.Push(value);
        

        for (int i = 0; i < tempStack.Count; ++i)
        {
            _stack.Push(tempStack.Pop());
        }
    }

    private static bool IsFalsey(Value value)
    {
        return value.IsNil() || (value.IsBool() && !value.AsBool());
    }

    private void RuntimeError(string message)
    {
        Console.Error.WriteLine(message);

        Console.Error.WriteLine($"{_chunk.GetLine(PC)} in script");
    }

    private byte ReadByte()
    {
        return _chunk.Code[PC++];
    }

    private ushort ReadShort()
    {
        return BitConverter.ToUInt16([ReadByte(), ReadByte()]);
    }

    private ushort ReadJumpShort()
    {
        ushort ret = (ushort)((_chunk.Code[PC] << 8) | _chunk.Code[PC + 1]);
        PC += 2;
        return ret;
    }

    private int ReadInt()
    {
        return BitConverter.ToInt32([ReadByte(), ReadByte(), ReadByte(), ReadByte()]);
    }

    private int ReadRegister()
    {
        return ReadInt();
    }

    private Value ReadConstant()
    {
        return _chunk.Constants.Values[ReadInt()];
        //if (_chunk.Constants.Values.Count <= byte.MaxValue)
        //    return _chunk.Constants.Values[ReadByte()];
        //else
        //    return _chunk.Constants.Values[ReadInt()];
    }

    private void StringBinaryOp()
    {
        int reg1 = ReadInt();
        int reg2 = ReadInt();

        string b = _registers[reg1].AsString();
        string a = _registers[reg2].AsString();

        _stack.Push(Value.StringVal(a + b));
    }

    private void BinaryOp(char op)
    {
        int reg = ReadInt();
        int mem;

        Value a, b;
        double na, nb;

        bool hasString;

        if (reg < 0)
        {
            RuntimeError("Cannot store binary result in constant.");
            _hadRTE = true;
            return;
        }

        mem = ReadInt();

        a = _registers[reg];
        b = mem >= 0 ? _registers[mem] : _chunk.ReadConstant(mem);

        if (op == '=')
        {
            _registers[reg] = Value.BoolVal(Value.ValuesEqual(a, b));
            return;
        }

        if (!a.IsString() && !a.IsNumber() && !b.IsString() && !b.IsNumber())
        {
            RuntimeError("Operands must be a number or a string");
            _hadRTE = true;
            return;
        }

        hasString = a.IsString() || b.IsString();
        if (hasString && op != '+')
        {
            RuntimeError("Strings can only be added together or compared by equality.");
            _hadRTE = true;
            return;
        }

        na = a.AsNumber();
        nb = b.AsNumber();

        switch (op)
        {
            case '<':
                _registers[reg] = Value.BoolVal(na < nb);
                break;
            case '>':
                _registers[reg] = Value.BoolVal(na > nb);
                break;
            case '+':
                _registers[reg] = hasString ? Value.StringVal(a.ToString() + b.ToString()) : Value.NumberVal(na + nb);
                break;
            case '-':
                _registers[reg] = Value.NumberVal(na - nb);
                break;
            case '*':
                _registers[reg] = Value.NumberVal(na * nb);
                break;
            case '/':
                _registers[reg] = Value.NumberVal(na / nb);
                break;
        }
    }
}
