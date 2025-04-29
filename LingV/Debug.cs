using System;

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
                return SimpleInstruction("OP_EQUAL", offset);
            case (byte)OpCode.OP_GREATER:
                return SimpleInstruction("OP_GREATER", offset);
            case (byte)OpCode.OP_LESS:
                return SimpleInstruction("OP_LESS", offset);
            case (byte)OpCode.OP_ADD:
                return SimpleInstruction("OP_ADD", offset);
            case (byte)OpCode.OP_SUBTRACT:
                return SimpleInstruction("OP_SUBTRACT", offset);
            case (byte)OpCode.OP_MULTIPLY:
                return SimpleInstruction("OP_MULTIPLY", offset);
            case (byte)OpCode.OP_DIVIDE:
                return SimpleInstruction("OP_DIVIDE", offset);
            case (byte)OpCode.OP_NOT:
                return SimpleInstruction("OP_NOT", offset);
            case (byte)OpCode.OP_NEGATE:
                return SimpleInstruction("OP_NEGATE", offset);
            case (byte)OpCode.OP_PRINT:
                return SimpleInstruction("OP_PRINT", offset);
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

    private static int ConstantInstruction(string name, Chunk chunk, int offset)
    {
        int constant = BitConverter.ToInt32([chunk.Code[offset + 1], chunk.Code[offset + 2], chunk.Code[offset + 3], chunk.Code[offset + 4]]);
        Console.Write($"{name,-16} {constant:G4} '");
        Console.WriteLine($"{chunk.Constants.Values[constant]:G}'");

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
