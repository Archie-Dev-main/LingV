using System;
using System.Buffers.Binary;
using System.Text;
using System.Threading.Tasks;

namespace LingV;

public enum OpCode : byte
{
    OP_CONSTANT,
    //OP_CONSTANT_LONG,
    OP_NIL,
    OP_TRUE,
    OP_FALSE,
    OP_POP,
    OP_GET_LOCAL,
    OP_SET_LOCAL,
    OP_GET_GLOBAL,
    //OP_GET_GLOBAL_LONG,
    OP_DEFINE_GLOBAL,
    //OP_DEFINE_GLOBAL_LONG,
    OP_SET_GLOBAL,
    //OP_SET_GLOBAL_LONG,
    OP_EQUAL,
    OP_GREATER,
    OP_LESS,
    OP_ADD,
    OP_SUBTRACT,
    OP_MULTIPLY,
    OP_DIVIDE,
    OP_NOT,
    OP_NEGATE,
    OP_PRINT,
    OP_JUMP,
    OP_JUMP_IF_FALSE,
    OP_LOOP,
    OP_RETURN
}

public class Chunk
{
    public readonly List<byte> Code = [];
    public readonly ValueList Constants = new();

    private readonly List<int> _lines = [];
    private readonly List<int> _numRefs = [];

    public void Write(byte b, int line)
    {
        Code.Add(b);
        AddLine(line);
    }

    public void Write(byte[] bytes, int line)
    {
        Code.AddRange(bytes);
        AddLine(line);
    }

    public int WriteConstant(Value value, int line)
    {
        int constant = AddConstant(value);
        byte[] bytes = BitConverter.GetBytes(constant);

        Write((byte)OpCode.OP_CONSTANT, line);
        Write(bytes, line);

        //if (Constants.Values.Count <= byte.MaxValue)
        //{
        //    Write((byte)OpCode.OP_CONSTANT, line);
        //    Write(bytes[0], line);
        //}
        //else
        //{
        //    Write((byte)OpCode.OP_CONSTANT_LONG, line);
        //    Write(bytes, line);
        //}

        AddLine(line);
        
        return constant;
    }

    public int AddConstant(Value value)
    {
        Constants.Write(value);

        return Constants.Values.Count - 1;
    }

    public int GetLine(int offset)
    {
        int count = 0;

        for (int i = 0; i < _numRefs.Count; ++i)
        {
            if (count + _numRefs[i] > offset)
            {
                return _lines[i];
            }
            else if (count == offset)
            {
                return _lines[i];
            }

            count += _numRefs[i];
        }

        return -1;
    }
    
    //public void PrintRLE()
    //{
    //    for (int i = 0; i < _lines.Count; ++i)
    //    {
    //        Console.WriteLine($"{_lines[i]} {_numRefs[i]}" );
    //    }
    //}

    //public List<int> GetLines()
    //{
    //    return _lines;
    //}

    private void AddLine(int line)
    {
        for (int i = 0; i < _lines.Count; ++i)
        {
            if (_lines[i] == line)
            {
                _numRefs[i] += 1;
                return;
            }
        }

        _lines.Add(line);
        _numRefs.Add(1);
    }
}
