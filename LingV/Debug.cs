using System;
using System.Reflection.Metadata;

namespace LingV;

public class Debug
{
    public static void DisassembleChunk(Chunk chunk, string name)
    {
        Console.WriteLine(string.Format("== {0} ==", name));

        for (int offset = 0; offset < chunk.Code.Count;)
        {
            offset = DisassembleInstruction(chunk, offset);
        }
    }

    public static int DisassembleInstruction(Chunk chunk, int offset)
    {
        Console.Write($"{offset:D4} ");

        if (offset > 0 && chunk.GetLine(offset) == chunk.GetLine(offset - 1))
            Console.Write("   | ");
        else
            Console.Write($"{chunk.GetLine(offset):D4} ");

        byte instruction = chunk.Code[offset];

        switch (instruction)
        {
            case (byte)OpCode.OP_CONSTANT:
                return ConstantInstruction("OP_CONSTANT", chunk, offset);
            case (byte)OpCode.OP_REGISTER:
                return RegisterInstruction("OP_REGISTER", chunk, offset);
            //case (byte)OpCode.OP_CONSTANT_LONG:
            //    return ConstantLongInstruction("OP_CONSTANT_LONG", chunk, offset);
            case (byte)OpCode.OP_NIL:
                return SimpleInstruction("OP_NIL", offset);
            case (byte)OpCode.OP_TRUE:
                return SimpleInstruction("OP_TRUE", offset);
            case (byte)OpCode.OP_FALSE:
                return SimpleInstruction("OP_FALSE", offset);
            case (byte)OpCode.OP_POP:
                return SimpleInstruction("OP_POP", offset);
            case (byte)OpCode.OP_MOV:
                return BinaryInstruction("OP_MOV", chunk, offset);
            case (byte)OpCode.OP_GET_LOCAL:
                return IntInstruction("OP_GET_LOCAL", chunk, offset);
            case (byte)OpCode.OP_SET_LOCAL:
                return IntInstruction("OP_SET_LOCAL", chunk, offset);
            case (byte)OpCode.OP_GET_GLOBAL:
                return ConstantInstruction("OP_GET_GLOBAL", chunk, offset);
            //case (byte)OpCode.OP_GET_GLOBAL_LONG:
            //    return ConstantLongInstruction("OP_GET_GLOBAL_LONG", chunk, offset);
            case (byte)OpCode.OP_DEFINE_GLOBAL:
                return ConstantInstruction("OP_DEFINE_GLOBAL", chunk, offset);
            //case (byte)OpCode.OP_DEFINE_GLOBAL_LONG:
            //    return ConstantLongInstruction("OP_DEFINE_GLOBAL_LONG", chunk, offset);
            case (byte)OpCode.OP_SET_GLOBAL:
                return ConstantInstruction("OP_SET_GLOBAL", chunk, offset);
            //case (byte)OpCode.OP_SET_GLOBAL_LONG:
            //    return ConstantLongInstruction("OP_SET_GLOBAL_LONG", chunk, offset);
            case (byte)OpCode.OP_EQUAL:
                return BinaryInstruction("OP_EQUAL", chunk, offset);
            case (byte)OpCode.OP_GREATER:
                return BinaryInstruction("OP_GREATER", chunk, offset);
            case (byte)OpCode.OP_LESS:
                return BinaryInstruction("OP_LESS", chunk, offset);
            case (byte)OpCode.OP_ADD:
                return BinaryInstruction("OP_ADD", chunk, offset);
            case (byte)OpCode.OP_SUBTRACT:
                return BinaryInstruction("OP_SUBTRACT", chunk, offset);
            case (byte)OpCode.OP_MULTIPLY:
                return BinaryInstruction("OP_MULTIPLY", chunk, offset);
            case (byte)OpCode.OP_DIVIDE:
                return BinaryInstruction("OP_DIVIDE", chunk, offset);
            case (byte)OpCode.OP_NOT:
                return UnaryInstruction("OP_NOT", chunk, offset);
            case (byte)OpCode.OP_NEGATE:
                return UnaryInstruction("OP_NEGATE", chunk, offset);
            case (byte)OpCode.OP_PRINT:
                return UnaryInstruction("OP_PRINT", chunk, offset);
            case (byte)OpCode.OP_JUMP:
                return JumpInstruction("OP_JUMP", 1, chunk, offset);
            case (byte)OpCode.OP_JUMP_IF_FALSE:
                return JumpInstruction("OP_JUMP_IF_FALSE", 1, chunk, offset);
            case (byte)OpCode.OP_LOOP:
                return JumpInstruction("OP_LOOP", -1, chunk, offset);
            case (byte)OpCode.OP_RETURN:
                return SimpleInstruction("OP_RETURN", offset);
            default:
                Console.WriteLine($"Unknown opcode {instruction}");
                return offset + 1;
        }
    }

    private static int JumpInstruction(string name, int sign, Chunk chunk, int offset)
    {
        ushort jump = (ushort)(chunk.Code[offset + 1] << 8);
        jump |= chunk.Code[offset + 2];
        Console.WriteLine($"{name,-16} {offset:D4} -> {offset + 3 + sign * jump:D4}");

        return offset + 3;
    }

    private static int SimpleInstruction(string name, int offset)
    {
        Console.WriteLine($"{name}");

        return offset + 1;
    }

    private static int UnaryInstruction(string name, Chunk chunk, int offset)
    {
        int op = BitConverter.ToInt32([chunk.Code[offset + 1], chunk.Code[offset + 2], chunk.Code[offset + 3], chunk.Code[offset + 4]]);

        Console.WriteLine($"{name,-16} r{op:G4}");

        return offset + 5;
    }

    private static int BinaryInstruction(string name, Chunk chunk, int offset)
    {
        int currOffset = offset;
        int op1 = BitConverter.ToInt32([chunk.Code[++currOffset], chunk.Code[++currOffset], chunk.Code[++currOffset], chunk.Code[++currOffset]]);
        int op2 = BitConverter.ToInt32([chunk.Code[++currOffset], chunk.Code[++currOffset], chunk.Code[++currOffset], chunk.Code[++currOffset]]);

        Value value = op2 < 0 ? chunk.ReadConstant(op2) : Value.NumberVal(op2);
        char op2Char = '$';

        if (op2 >= 0)
        {
            op2Char = 'r';
        }

        Console.WriteLine($"{name,-16} r{op1} {op2Char}{value}");

        return offset + 1 + 4 + 4;
    }

    private static int ConstantInstruction(string name, Chunk chunk, int offset)
    {
        int constant = BitConverter.ToInt32([chunk.Code[offset + 1], chunk.Code[offset + 2], chunk.Code[offset + 3], chunk.Code[offset + 4]]);
        Console.Write($"{name,-16} {constant:G4} '");
        Console.WriteLine($"{chunk.ReadConstant(constant):G}'");

        return offset + 5;
    }

    private static int RegisterInstruction(string name, Chunk chunk, int offset)
    {
        int constant = BitConverter.ToInt32([chunk.Code[offset + 1], chunk.Code[offset + 2], chunk.Code[offset + 3], chunk.Code[offset + 4]]);
        Console.WriteLine($"{name,-16} {constant:G4}");// '");
        //Console.WriteLine($"{constant:G}'");

        return offset + 5;
    }

    private static int IntInstruction(string name, Chunk chunk, int offset)
    {
        int slot = BitConverter.ToInt32([chunk.Code[offset + 1], chunk.Code[offset + 2], chunk.Code[offset + 3], chunk.Code[offset + 4]]);
        Console.WriteLine($"{name,-16} {slot:G4}");

        return offset + 5;
    }

    //private static int ConstantLongInstruction(string name, Chunk chunk, int offset)
    //{
    //    int constant = BitConverter.ToInt32([chunk.Code[offset + 1], chunk.Code[offset + 2], chunk.Code[offset + 3], chunk.Code[offset + 4]]);
    //    Console.Write($"{name,-16} {constant:G4} '");
    //    Console.WriteLine($"{chunk.Constants.Values[constant]:G}'");

    //    return offset + 5;
    //}
}
