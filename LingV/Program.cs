// See https://aka.ms/new-console-template for more information
using LingV;
using System.Text;

VirtualMachine vm = new();

//Chunk chunk = new();

//for (int i = 0; i < 256; ++i)
//{
//    chunk.Constants.Write(i);
//}

//chunk.WriteConstant(Value.NumberVal(1.2), 123);

//chunk.WriteConstant(Value.NumberVal(3.4), 123);

//chunk.Write((byte)OpCode.OP_ADD, 123);

//chunk.WriteConstant(Value.NumberVal(5.6), 123);

//chunk.Write((byte)OpCode.OP_DIVIDE, 123);

//chunk.Write((byte)OpCode.OP_NEGATE, 123);

//chunk.Write((byte)OpCode.OP_RETURN, 123);

//vm.Interpret(chunk);

if (args.Length == 0)
    REPL(vm);
else if (args.Length == 1)
    RunFile(vm, args[0]);
else
{
    Console.Error.WriteLine("Usage: lingv <path>");
    Environment.Exit(64);
}

static void REPL(VirtualMachine vm)
{
    string line;

    for (; ;)
    {
        Console.Write(">");

        if ((line = Console.ReadLine()) != null)
        {
            Console.WriteLine();
            break;
        }

        vm.Interpret(line, 32);
    }
}

static void RunFile(VirtualMachine vm, string path)
{
    byte[] bytes;
    string source;

    try
    {
        bytes = File.ReadAllBytes(path);
    }
    catch
    {
        Console.WriteLine("Could not open file!");
        Environment.Exit(74);
        return;
    }

    source = Encoding.UTF8.GetString(bytes);

    source += '\0';

    InterpretResult result = vm.Interpret(source, 32);

    if (result == InterpretResult.INTERPRET_COMPILE_ERROR)
        Environment.Exit(65);

    if (result == InterpretResult.INTERPRET_RUNTIME_ERROR)
        Environment.Exit(70);
}