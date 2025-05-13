#define DEBUG_PRINT_CODE

using System;

namespace LingV;

public struct Parser
{
    public Token Current;
    public Token Previous;
    public bool HadError;
    public bool PanicMode;
}

public enum Precedence
{
    PREC_NONE,
    PREC_ASSIGNMENT,  // =
    PREC_OR,          // or
    PREC_AND,         // and
    PREC_EQUALITY,    // == !=
    PREC_COMPARISON,  // < > <= >=
    PREC_TERM,        // + -
    PREC_FACTOR,      // * /
    PREC_UNARY,       // ! -
    PREC_CALL,        // . ()
    PREC_PRIMARY
}

public struct ParseRule(Action<bool> prefix, Action<bool> infix, Precedence precedence)
{
    public Action<bool> Prefix = prefix;
    public Action<bool> Infix = infix;
    public Precedence Precedence = precedence;
}

public struct Local(Token name, int depth)
{
    public Token Name = name;
    public int Depth = depth;
}

public class ExpressionQueue
{
    public int ExpressionDepth
    {
        get
        {
            return _expressionOrder.Count;
        }
    }

    private readonly Stack<Queue<int>> _expressionOrder = [];
    private Queue<int> _currentOrder
    {
        get
        {
            return _expressionOrder.Peek();
        }
    }

    public void PushExpression()
    {
        _expressionOrder.Push([]);
    }

    public void PopExpression()
    {
        Queue<int> temp = _expressionOrder.Pop();

        if (_expressionOrder.Count == 0)
            return;

        _currentOrder.Enqueue(temp.Dequeue());
    }

    public void AddValue(int value)
    {
        //Console.WriteLine($"Value Added: {value}");
        _currentOrder.Enqueue(value);
    }

    public int GetValue()
    {
        int value = _currentOrder.Dequeue();

        //Console.WriteLine($"Value Removed: {value}");

        return value;
    }
}

public class RegisterSelector
{
    public Action<int> EvictRegister;

    private readonly int _gpRegNum;
    private readonly List<int> _usedRegisters = [];
    private readonly List<int> _unusedRegisters = [];

    public RegisterSelector(int gpRegNum)
    {
        _gpRegNum = gpRegNum;

        for (int i = 0; i < gpRegNum; ++i)
        {
            _unusedRegisters.Add(i);
        }
    }

    public int GetUnusedRegister()
    {
        int reg;

        if (_unusedRegisters.Count == 0)
        {
            EvictRegister(_usedRegisters[0]);
            _unusedRegisters.Add(_usedRegisters[0]);
            _usedRegisters.RemoveAt(0);
        }
        
        reg = _unusedRegisters[0];
        _unusedRegisters.RemoveAt(0);
        SetRecentlyUsedRegister(reg);

        return reg;
    }

    public int GetMostRecentlyUsedRegister()
    {
        SetRecentlyUsedRegister(_usedRegisters[^1]);
        return _usedRegisters[^1];
    }

    private void SetRecentlyUsedRegister(int reg)
    {
        int idx;

        if (_usedRegisters.Contains(reg))
        {
            idx = _usedRegisters.Find(i => i == reg);
            _usedRegisters.RemoveAt(idx);
            _usedRegisters.Add(idx);
            return;
        }

        _usedRegisters.Add(reg);
    }
}

public class Compiler
{
    private Scanner _scanner;
    private Parser _parser = new();
    private Chunk _currentChunk;

    private readonly Stack<int> _breakJumps = [];

    private readonly Queue<int> _expressionOrder = new();
    private int _expressionCount = 0;

    private int _register = -1;

    private readonly ExpressionQueue _expressionQueue = new();
    private readonly RegisterSelector _regSelector;

    //private readonly List<Local> _locals = [];
    //private int _scopeDepth = 0;

    private readonly Dictionary<TokenType, ParseRule> _rules;

    public Compiler(int gpRegNum)
    {
         _regSelector = new(gpRegNum);

        _rules = new ()
        {
            { TokenType.TOKEN_LEFT_PAREN,       new(Grouping,   null,   Precedence.PREC_NONE)  },
            { TokenType.TOKEN_RIGHT_PAREN,      new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_LEFT_BRACE,       new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_RIGHT_BRACE,      new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_COMMA,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_DOT,              new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_MINUS,            new(Unary,      Binary, Precedence.PREC_TERM) },
            { TokenType.TOKEN_PLUS,             new(null,       Binary, Precedence.PREC_TERM) },
            { TokenType.TOKEN_SEMICOLON,        new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_SLASH,            new(null,       Binary, Precedence.PREC_FACTOR) },
            { TokenType.TOKEN_STAR,             new(null,       Binary, Precedence.PREC_FACTOR) },
            { TokenType.TOKEN_BANG,             new(Unary,      null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_BANG_EQUAL,       new(null,       Binary, Precedence.PREC_EQUALITY) },
            { TokenType.TOKEN_EQUAL,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_EQUAL_EQUAL,      new(null,       Binary, Precedence.PREC_EQUALITY) },
            { TokenType.TOKEN_GREATER,          new(null,       Binary, Precedence.PREC_COMPARISON) },
            { TokenType.TOKEN_GREATER_EQUAL,    new(null,       Binary, Precedence.PREC_COMPARISON) },
            { TokenType.TOKEN_LESS,             new(null,       Binary, Precedence.PREC_COMPARISON) },
            { TokenType.TOKEN_LESS_EQUAL,       new(null,       Binary, Precedence.PREC_COMPARISON) },
            { TokenType.TOKEN_IDENTIFIER,       new(null,   null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_STRING,           new(String,     null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_NUMBER,           new(Number,     null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_AND,              new(null,       null,    Precedence.PREC_NONE) },
            { TokenType.TOKEN_CLASS,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_ELSE,             new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_FALSE,            new(Literal,    null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_FOR,              new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_FUN,              new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_IF,               new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_NIL,              new(Literal,    null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_OR,               new(null,       null,     Precedence.PREC_NONE) },
            { TokenType.TOKEN_PRINT,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_RETURN,           new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_SUPER,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_THIS,             new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_TRUE,             new(Literal,    null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_VAR,              new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_WHILE,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_BREAK,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_ERROR,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_EOF,              new(null,       null,   Precedence.PREC_NONE) },
        };
    }

    public bool Compile(string source, Chunk chunk)
    {
        _scanner = new(source);
        _currentChunk = chunk;

        _parser.HadError = false;
        _parser.PanicMode = false;

        //for (; ; )
        //{
        //    Token token = _scanner.ScanToken();
        //    if (token.Line != _scanner.GetLine())
        //    {
        //        Console.WriteLine($"{token.Line:D4}");
        //        _scanner.SetLine(token.Line);
        //    }
        //    else
        //    {
        //        Console.WriteLine("   | ");
        //    }

        //    Console.WriteLine($"{token.Type} '{token.Lexeme}'");

        //    if (token.Type == TokenType.TOKEN_EOF)
        //        break;
        //}
        //return false;

        Advance();
        Expression();
        Consume(TokenType.TOKEN_EOF, "Expect end of expression.");

        //while (!Match(TokenType.TOKEN_EOF))
        //    Declaration();

        EndCompiler();

        return !_parser.HadError;
    }

    private void Advance()
    {
        _parser.Previous = _parser.Current;

        for (;;)
        {
            _parser.Current = _scanner.ScanToken();

            if (_parser.Current.Type != TokenType.TOKEN_ERROR)
                break;

            ErrorAtCurrent(_parser.Current.Lexeme);
        }

        //Console.WriteLine($"parser previous : {_parser.Previous.Type}");
        //Console.WriteLine($"parser current : {_parser.Current.Type}");
    }

    private void Consume(TokenType type, string message)
    {
        //Console.WriteLine($"desired type : {type}");
        //Console.WriteLine($"current type : {_parser.Current.Type}");

        if (_parser.Current.Type == type)
        {
            Advance();
            return;
        }

        ErrorAtCurrent(message);
    }

    private bool Match(TokenType type)
    {
        if (!Check(type))
            return false;

        Advance();
        return true;
    }

    private bool Check(TokenType type)
    {
        return _parser.Current.Type == type;
    }

    private void EmitByte(byte b)
    {
        _currentChunk.Write(b, _parser.Previous.Line);
    }

    private void EmitBytes(byte b1, byte b2)
    {
        EmitByte(b1);
        EmitByte(b2);
    }

    private void EmitBytes(byte[] bytes)
    {
        _currentChunk.Write(bytes, _parser.Previous.Line);
    }

    private void EmitBinaryInstruction(byte instruction)
    {
        EmitByte(instruction);
        EmitRegister(_expressionQueue.GetValue());
        EmitRegister(_expressionQueue.GetValue());
        _expressionQueue.AddValue(_register);
    }

    private void EmitUnaryInstruction(byte instruction)
    {
        EmitByte(instruction);
        EmitRegister(_register);
    }

    //private void EmitLoop(int loopStart)
    //{
    //    EmitByte((byte)OpCode.OP_LOOP);

    //    int offset = _currentChunk.Code.Count - loopStart + 2;
    //    if (offset > ushort.MaxValue)
    //        Error("Loop body too large.");

    //    EmitByte((byte)((offset >> 8) & 0xff));
    //    EmitByte((byte)(offset & 0xff));
    //}

    private int EmitJump(byte instuction)
    {
        EmitByte(instuction);
        EmitByte(0xFF);
        EmitByte(0xFF);

        return _currentChunk.Code.Count - 2;
    }

    private void EmitReturn()
    {
        EmitByte((byte)OpCode.OP_RETURN);
    }

    private void EmitConstant(Value value)
    {
        if (_expressionCount == 0)
        {
            _expressionQueue.AddValue(_register);
            _expressionQueue.AddValue(_currentChunk.WriteConstant(value, _parser.Previous.Line));
            EmitBinaryInstruction((byte)OpCode.OP_MOV);
            //_expressionQueue.AddValue(_register);
        }
        else
            _expressionQueue.AddValue(_currentChunk.WriteConstant(value, _parser.Previous.Line));

        //_expressionOrder.Enqueue(_currentChunk.WriteConstant(value, _parser.Previous.Line));
        _expressionCount++;
    }

    private void EmitRegister(int reg)
    {
        //EmitByte((byte)OpCode.OP_REGISTER);
        EmitBytes(BitConverter.GetBytes(reg));
    }

    //private void PatchJump(int offset)
    //{
    //    int jump = _currentChunk.Code.Count - 2 - offset;

    //    if (jump > ushort.MaxValue)
    //    {
    //        Error("Too much code to jump over.");
    //    }

    //    _currentChunk.Code[offset] = (byte)((jump >> 8) & 0xff);
    //    _currentChunk.Code[offset + 1] = (byte)(jump & 0xff);

    //    ushort test = BitConverter.ToUInt16([_currentChunk.Code[offset + 1], _currentChunk.Code[offset]]);
    //    Console.WriteLine($"test={test}");
    //}

    private void EndCompiler()
    {
        EmitReturn();

        //while (_expressionOrder.Count > 0)
        //{
        //    int index = _expressionOrder.Dequeue();
        //    Console.WriteLine($"index: {index}");
        //    Console.WriteLine($"constant: {_currentChunk.ReadConstant(index)}");
        //}

#if DEBUG_PRINT_CODE
        if (!_parser.HadError)
            Debug.DisassembleChunk(_currentChunk, "code");
#endif
    }

    //private void BeginScope()
    //{
    //    _scopeDepth++;
    //}

    //private void EndScope()
    //{
    //    _scopeDepth--;

    //    while (_locals.Count > 0 && _locals[^1].Depth > _scopeDepth)
    //    {
    //        EmitByte((byte)OpCode.OP_POP);
    //        _locals.RemoveAt(_locals.Count - 1);
    //    }
    //}

    private void Grouping(bool canAssign)
    {
        Expression();
        Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after expression.");
    }

    private void Number(bool canAssign)
    {
        double value = double.Parse(_parser.Previous.Lexeme);
        EmitConstant(Value.NumberVal(value));
    }

    private void String(bool canAssign)
    {
        EmitConstant(Value.StringVal(_parser.Previous.Lexeme));
    }

    //private void Or(bool canAssign)
    //{
    //    int elseJump = EmitJump((byte)OpCode.OP_JUMP_IF_FALSE);
    //    int endJump = EmitJump((byte)OpCode.OP_JUMP);

    //    PatchJump(elseJump);
    //    EmitByte((byte)OpCode.OP_POP);

    //    ParsePrecedence(Precedence.PREC_OR);
    //    PatchJump(endJump);
    //}

    private void Binary(bool canAssign)
    {
        TokenType opType = _parser.Previous.Type;
        ParseRule rule = _rules[opType];

        ParsePrecedence(rule.Precedence + 1);

        switch (opType)
        {
            case TokenType.TOKEN_BANG_EQUAL:
                EmitBinaryInstruction((byte)OpCode.OP_EQUAL);
                EmitUnaryInstruction((byte)OpCode.OP_NOT);
                break;
            case TokenType.TOKEN_EQUAL_EQUAL:
                EmitBinaryInstruction((byte)OpCode.OP_EQUAL);
                break;
            case TokenType.TOKEN_GREATER:
                EmitBinaryInstruction((byte)OpCode.OP_GREATER);
                break;
            case TokenType.TOKEN_GREATER_EQUAL:
                EmitBinaryInstruction((byte)OpCode.OP_LESS);
                EmitUnaryInstruction((byte)OpCode.OP_NOT);
                break;
            case TokenType.TOKEN_LESS:
                EmitBinaryInstruction((byte)OpCode.OP_LESS);
                break;
            case TokenType.TOKEN_LESS_EQUAL:
                EmitBinaryInstruction((byte)OpCode.OP_GREATER);
                EmitUnaryInstruction((byte)OpCode.OP_NOT);
                break;
            case TokenType.TOKEN_PLUS:
                EmitBinaryInstruction((byte)OpCode.OP_ADD);
                break;
            case TokenType.TOKEN_MINUS:
                EmitBinaryInstruction((byte)OpCode.OP_SUBTRACT);
                break;
            case TokenType.TOKEN_STAR:
                EmitBinaryInstruction((byte)OpCode.OP_MULTIPLY);
                break;
            case TokenType.TOKEN_SLASH:
                EmitBinaryInstruction((byte)OpCode.OP_DIVIDE);
                break;
            default:
                return;
        }
    }

    private void Literal(bool canAssign)
    {
        switch (_parser.Previous.Type)
        {
            case TokenType.TOKEN_FALSE: EmitByte((byte)OpCode.OP_FALSE); break;
            case TokenType.TOKEN_NIL: EmitByte((byte)OpCode.OP_NIL); break;
            case TokenType.TOKEN_TRUE: EmitByte((byte)OpCode.OP_TRUE); break;
            default: return;
        }
    }

    private void Unary(bool canAssign)
    {
        TokenType opType = _parser.Previous.Type;

        ParsePrecedence(Precedence.PREC_UNARY);

        switch (opType)
        {
            case TokenType.TOKEN_MINUS: EmitUnaryInstruction((byte)OpCode.OP_NEGATE); break;
            default:
                return;
        }
    }

    private void ParsePrecedence(Precedence precedence)
    {
        Advance();

        Action<bool> prefixRule = _rules[_parser.Previous.Type].Prefix;

        if (prefixRule == null)
        {
            Error("Expect expression.");
            return;
        }

        bool canAssign = precedence <= Precedence.PREC_ASSIGNMENT;
        prefixRule(canAssign);

        while (precedence <= _rules[_parser.Current.Type].Precedence)
        {
            Advance();
            Action<bool> infixRule = _rules[_parser.Previous.Type].Infix;
            infixRule(canAssign);
        }

        if (canAssign && Match(TokenType.TOKEN_EQUAL))
        {
            Error("Invalid assignment target.");
        }
    }

    //private void And(bool canAsign)
    //{
    //    int endJump = EmitJump((byte)OpCode.OP_JUMP_IF_FALSE);

    //    EmitByte((byte)OpCode.OP_POP);
    //    ParsePrecedence(Precedence.PREC_AND);

    //    PatchJump(endJump);
    //}

    private void Expression()
    {
        _expressionQueue.PushExpression();
        _register++;
        _expressionCount = 0;
        ParsePrecedence(Precedence.PREC_ASSIGNMENT);
        _expressionQueue.PopExpression();
        //_expressionQueue.AddValue(_register);
        _register--;
    }

    //private void Block()
    //{
    //    while (!Check(TokenType.TOKEN_RIGHT_BRACE) && !Check(TokenType.TOKEN_EOF))
    //    {
    //        Declaration();
    //    }

    //    Consume(TokenType.TOKEN_RIGHT_BRACE, "Expect '}' after block.");
    //}

    private void ExpressionStatement()
    {
        Expression();
        Consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after expression.");
        //EmitByte((byte)OpCode.OP_POP);
    }

    //private void ForStatement()
    //{
    //    BeginScope();
    //    Consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after 'for'.");
        
    //    if (Match(TokenType.TOKEN_SEMICOLON))
    //    {

    //    }
    //    else if (Match(TokenType.TOKEN_VAR))
    //    {
    //        VarDeclaration();
    //    }
    //    else
    //    {
    //        ExpressionStatement();
    //    }

    //    int loopStart = _currentChunk.Code.Count;

    //    int exitJump = -1;
    //    if (!Match(TokenType.TOKEN_SEMICOLON))
    //    {
    //        Expression();
    //        Consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after loop condition.");

    //        exitJump = EmitJump((byte)OpCode.OP_JUMP_IF_FALSE);
    //        EmitByte((byte)OpCode.OP_POP);
    //    }

    //    if (!Match(TokenType.TOKEN_RIGHT_PAREN))
    //    {
    //        int bodyJump = EmitJump((byte)OpCode.OP_JUMP);
    //        int incrementStart = _currentChunk.Code.Count;
    //        Expression();
    //        EmitByte((byte)OpCode.OP_POP);
    //        Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after for clauses.");

    //        EmitLoop(loopStart);
    //        loopStart = incrementStart;
    //        PatchJump(bodyJump);
    //    }

    //    Statement();
    //    EmitLoop(loopStart);

    //    if (exitJump != -1)
    //    {
    //        PatchJump(exitJump);
    //        _breakJumps.Push(exitJump);
    //        EmitByte((byte)OpCode.OP_POP);
    //    }

    //    EndScope();
    //}

    //private void IfStatement()
    //{
    //    Consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after 'if'.");
    //    Expression();
    //    Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after condition.");

    //    int thenJump = EmitJump((byte)OpCode.OP_JUMP_IF_FALSE);
    //    EmitByte((byte)OpCode.OP_POP);
    //    Statement();

    //    int elseJump = EmitJump((byte)OpCode.OP_JUMP);

    //    PatchJump(thenJump);
    //    EmitByte((byte)OpCode.OP_POP);

    //    if (Match(TokenType.TOKEN_ELSE))
    //        Statement();

    //    PatchJump(elseJump);
    //}

    private void PrintStatement()
    {
        Expression();
        Consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after value.");
        EmitByte((byte)OpCode.OP_PRINT);
    }

    //private void WhileStatement()
    //{
    //    int loopStart = _currentChunk.Code.Count;
    //    Consume(TokenType.TOKEN_LEFT_PAREN, "Expect '(' after 'while'.");
    //    Expression();
    //    Consume(TokenType.TOKEN_RIGHT_PAREN, "Expect ')' after condition.");

    //    int exitJump = EmitJump((byte)OpCode.OP_JUMP_IF_FALSE);
    //    EmitByte((byte)OpCode.OP_POP);
    //    Statement();
    //    EmitLoop(loopStart);

    //    PatchJump(exitJump);
    //    _breakJumps.Push(exitJump);
    //    EmitByte((byte)OpCode.OP_POP);
    //}

    //private void BreakStatement()
    //{
    //    if (_breakJumps.Count == 0)
    //    {
    //        Error("Break statement has to be in a loop.");
    //        return;
    //    }

    //    EmitJump((byte)OpCode.OP_JUMP);
    //    //EmitByte((byte)OpCode.OP_POP);

    //    int jumpTo = _breakJumps.Pop();
    //    PatchJump(jumpTo);
    //}

    private void Synchronize()
    {
        _parser.PanicMode = false;

        while (_parser.Current.Type == TokenType.TOKEN_SEMICOLON)
        {
            if (_parser.Previous.Type == TokenType.TOKEN_SEMICOLON)
                return;

            switch (_parser.Current.Type )
            {
                case TokenType.TOKEN_CLASS:
                case TokenType.TOKEN_FUN:
                case TokenType.TOKEN_VAR:
                case TokenType.TOKEN_FOR:
                case TokenType.TOKEN_IF:
                case TokenType.TOKEN_WHILE:
                case TokenType.TOKEN_PRINT:
                case TokenType.TOKEN_RETURN:
                    return;
                default:
                    break;
            }

            Advance();
        }
    }

    //private void Declaration()
    //{
    //    if (Match(TokenType.TOKEN_VAR))
    //        VarDeclaration();
    //    else
    //        Statement();

    //    if (_parser.PanicMode)
    //        Synchronize();
    //}

    private void Statement()
    {
        Console.WriteLine($"{_parser.Current.Type}");

        if (Match(TokenType.TOKEN_PRINT))
            PrintStatement();
        //else if (Match(TokenType.TOKEN_FOR))
        //    ForStatement();
        //else if (Match(TokenType.TOKEN_IF))
        //    IfStatement();
        //else if (Match(TokenType.TOKEN_WHILE))
        //    WhileStatement();
        //else if (Match(TokenType.TOKEN_BREAK))
        //    BreakStatement();
        else if (Match(TokenType.TOKEN_LEFT_BRACE))
        {
            //BeginScope();
            //Block();
            //EndScope();
        }
        else
            ExpressionStatement();
    }

    private void EvictRegister(int reg)
    {
        EmitByte((byte)OpCode.OP_PUSH);
        EmitRegister(reg);
    }

    private void ErrorAtCurrent(string message)
    {
        ErrorAt(_parser.Current, message);
    }

    private void Error(string message)
    {
        ErrorAt(_parser.Previous, message);
    }

    private void ErrorAt(Token token, string message)
    {
        if (_parser.PanicMode)
            return;

        _parser.PanicMode = true;

        Console.Error.WriteLine($"[line {token.Line}] Error");

        if (token.Type == TokenType.TOKEN_EOF)
        {
            Console.Error.WriteLine(" at end");
        }
        else if (token.Type == TokenType.TOKEN_ERROR)
        {
            // Intentional nothing
        }
        else
        {
            Console.Error.WriteLine($" at {token.Lexeme}");
        }

        Console.Error.WriteLine($": {message}");
        _parser.HadError = true;
    }
}
