#define DEBUG_TRACE_EXECUTION

using System;

namespace LingV;

public enum InterpretResult
{
    INTERPRET_OK,
    INTERPRET_COMPILE_ERROR,
    INTERPRET_RUNTIME_ERROR
}

public class VirtualMachine
{
    private int PC = 0;
    private Chunk _chunk;
    private readonly Stack<Value> _stack = [];
    private readonly Dictionary<string, Value> _globals = [];

    public InterpretResult Interpret(string source)
    {
        Chunk chunk = new();
        Compiler compiler = new();

        if (!compiler.Compile(source, chunk))
            return InterpretResult.INTERPRET_COMPILE_ERROR;

        _chunk = chunk;
        PC = 0;

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
            string name;
            Value value;
            int slot;
            ushort offset;

            switch (instruction)
            {
                case (byte)OpCode.OP_CONSTANT:
                    Value constant = ReadConstant();
                    _stack.Push(constant);
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
                case (byte)OpCode.OP_POP:
                    _stack.Pop();
                    break;
                case (byte)OpCode.OP_GET_LOCAL:
                    slot = ReadInt();

                    _stack.Push(_stack.Peek());
                    break;
                case (byte)OpCode.OP_SET_LOCAL:
                    slot = ReadInt();

                    InsertInStack(slot, _stack.Peek());
                    break;
                case (byte)OpCode.OP_GET_GLOBAL:
                //case (byte)OpCode.OP_GET_GLOBAL_LONG:
                    name = ReadConstant().AsString();

                    if (_globals.TryGetValue(name, out value))
                    {
                        _stack.Push(value);
                    }
                    else
                    {
                        RuntimeError($"Undefined variable '{name}'.");
                        return InterpretResult.INTERPRET_RUNTIME_ERROR;
                    }

                    break;
                case (byte)OpCode.OP_DEFINE_GLOBAL:
                //case (byte)OpCode.OP_DEFINE_GLOBAL_LONG:
                    name = ReadConstant().AsString();

                    //if (_globals.TryGetValue(name, out value))
                    //{
                    //    RuntimeError($"Variable '{name}' cannot be defined twice.");
                    //    return InterpretResult.INTERPRET_RUNTIME_ERROR;
                    //}

                    _globals[name] = Peek(0);
                    _stack.Pop();
                    break;
                case (byte)OpCode.OP_SET_GLOBAL:
                //case (byte)OpCode.OP_SET_GLOBAL_LONG:
                    name = ReadConstant().AsString();

                    if (_globals.TryGetValue(name, out value))
                    {
                        _globals[name] = Peek(0);
                    }
                    else
                    {
                        RuntimeError($"Undefined variable '{name}'.");
                        return InterpretResult.INTERPRET_RUNTIME_ERROR;
                    }

                    break;
                case (byte)OpCode.OP_EQUAL:
                    Value b = _stack.Pop();
                    Value a = _stack.Pop();

                    _stack.Push(Value.BoolVal(Value.ValuesEqual(a, b)));
                    break;
                case (byte)OpCode.OP_GREATER:
                    BinaryOp('>');
                    break;
                case (byte)OpCode.OP_LESS:
                    BinaryOp('<');
                    break;
                case (byte)OpCode.OP_ADD:
                    if (Peek(0).IsString() || Peek(1).IsString())
                    {
                        StringBinaryOp();
                    }   
                    else if (Peek(0).IsNumber() && Peek(1).IsNumber())
                    {
                        BinaryOp('+');
                    }
                    else
                    {
                        RuntimeError("Operands must be two numbers or two strings.");
                        return InterpretResult.INTERPRET_RUNTIME_ERROR;
                    }

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
                    _stack.Push(Value.BoolVal(IsFalsey(_stack.Pop())));
                    break;
                case (byte)OpCode.OP_NEGATE:
                    if (!Peek(0).IsNumber())
                    {
                        RuntimeError("Operand must be a number.");
                        return InterpretResult.INTERPRET_RUNTIME_ERROR;
                    }

                    _stack.Push(Value.NumberVal(-_stack.Pop().AsNumber()));
                    break;
                case (byte)OpCode.OP_PRINT:
                    _stack.Pop().PrintValue();
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
                    return InterpretResult.INTERPRET_OK;
            }
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
        string b = _stack.Pop().ToString();
        string a = _stack.Pop().ToString();

        _stack.Push(Value.StringVal(a + b));
    }

    private void BinaryOp(char op)
    {
        double b = _stack.Pop().AsNumber();
        double a = _stack.Pop().AsNumber();

        switch (op)
        {
            case '<':
                _stack.Push(Value.BoolVal(a < b));
                break;
            case '>':
                _stack.Push(Value.BoolVal(a > b));
                break;
            case '+':
                _stack.Push(Value.NumberVal(a + b));
                break;
            case '-':
                _stack.Push(Value.NumberVal(a - b));
                break;
            case '*':
                _stack.Push(Value.NumberVal(a * b));
                break;
            case '/':
                _stack.Push(Value.NumberVal(a / b));
                break;
        }
    }
}
