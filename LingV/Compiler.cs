#define DEBUG_PRINT_CODE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

public struct Local
{
    public Token name;
    public Token value;
}

public class Compiler
{
    private Scanner _scanner;
    private Parser _parser = new();
    private Chunk _currentChunk;

    private List<Local> _locals = [];
    private int _scopeDeath = 0;

    private readonly Dictionary<TokenType, ParseRule> _rules;

    public Compiler()
    {
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
            { TokenType.TOKEN_IDENTIFIER,       new(Variable,   null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_STRING,           new(String,     null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_NUMBER,           new(Number,     null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_AND,              new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_CLASS,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_ELSE,             new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_FALSE,            new(Literal,    null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_FOR,              new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_FUN,              new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_IF,               new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_NIL,              new(Literal,    null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_OR,               new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_PRINT,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_RETURN,           new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_SUPER,            new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_THIS,             new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_TRUE,             new(Literal,    null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_VAR,              new(null,       null,   Precedence.PREC_NONE) },
            { TokenType.TOKEN_WHILE,            new(null,       null,   Precedence.PREC_NONE) },
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
        //Expression();
        //Consume(TokenType.TOKEN_EOF, "Expect end of expression.");

        while (!Match(TokenType.TOKEN_EOF))
            Declaration();

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

    private void EmitReturn()
    {
        EmitByte((byte)OpCode.OP_RETURN);
    }

    private void EmitConstant(Value value)
    {
        _currentChunk.WriteConstant(value, _parser.Previous.Line);
    }

    private void EmitGlobalVarOp(OpCode normal, OpCode extended, int value)
    {
        if (value <= byte.MaxValue)
        {
            EmitBytes((byte)normal, (byte)value);
        }
        else
        {
            EmitByte((byte)extended);
            EmitBytes(BitConverter.GetBytes(value));
        }
    }

    private void EndCompiler()
    {
        EmitReturn();

#if DEBUG_PRINT_CODE
        if (!_parser.HadError)
            Debug.DisassembleChunk(_currentChunk, "code");
#endif
    }

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

    private void NamedVariable(Token name, bool canAssign)
    {
        int arg = FindIdentifierConstant(name);

        if (canAssign && arg >= 0 && Match(TokenType.TOKEN_EQUAL))
        {
            Expression();

            EmitGlobalVarOp(OpCode.OP_SET_GLOBAL, OpCode.OP_SET_GLOBAL_LONG, arg);
        }
        else if (arg >= 0)
        {
            EmitGlobalVarOp(OpCode.OP_GET_GLOBAL, OpCode.OP_GET_GLOBAL_LONG, arg);
        }
    }

    private void Variable(bool canAssign)
    {
        NamedVariable(_parser.Previous, canAssign);
    }

    private void Binary(bool canAssign)
    {
        TokenType opType = _parser.Previous.Type;
        ParseRule rule = _rules[opType];

        ParsePrecedence(rule.Precedence + 1);

        switch (opType)
        {
            case TokenType.TOKEN_BANG_EQUAL:
                EmitBytes((byte)OpCode.OP_EQUAL, (byte)OpCode.OP_NOT);
                break;
            case TokenType.TOKEN_EQUAL_EQUAL:
                EmitByte((byte)OpCode.OP_EQUAL);
                break;
            case TokenType.TOKEN_GREATER:
                EmitByte((byte)OpCode.OP_GREATER);
                break;
            case TokenType.TOKEN_GREATER_EQUAL:
                EmitBytes((byte)OpCode.OP_LESS, (byte)OpCode.OP_NOT);
                break;
            case TokenType.TOKEN_LESS:
                EmitByte((byte)OpCode.OP_LESS);
                break;
            case TokenType.TOKEN_LESS_EQUAL:
                EmitBytes((byte)OpCode.OP_GREATER, (byte)OpCode.OP_NOT);
                break;
            case TokenType.TOKEN_PLUS:
                EmitByte((byte)OpCode.OP_ADD);
                break;
            case TokenType.TOKEN_MINUS:
                EmitByte((byte)OpCode.OP_SUBTRACT);
                break;
            case TokenType.TOKEN_STAR:
                EmitByte((byte)OpCode.OP_MULTIPLY);
                break;
            case TokenType.TOKEN_SLASH:
                EmitByte((byte)OpCode.OP_DIVIDE);
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
            case TokenType.TOKEN_MINUS: EmitByte((byte)OpCode.OP_NEGATE); break;
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

    private int IdentifierConstant(Token name)
    {
        return _currentChunk.AddConstant(Value.StringVal(name.Lexeme));
    }

    private int FindIdentifierConstant(Token name)
    {
        List<Value> constants = _currentChunk.Constants.Values;

        for (int i = 0; i < constants.Count; ++i)
        {
            Value v = constants[i];
            if (v.IsString() && v.AsString() == name.Lexeme)
                return i;
        }

        return -1;
    }

    private int ParseVariable(string errorMessage)
    {
        Consume(TokenType.TOKEN_IDENTIFIER, errorMessage);
        return IdentifierConstant(_parser.Previous);
    }

    private void DefineVariable(int global)
    {
        if (global <= byte.MaxValue)
        {
            EmitBytes((byte)OpCode.OP_DEFINE_GLOBAL, (byte)global);
        } 
        else
        {
            EmitByte((byte)OpCode.OP_DEFINE_GLOBAL_LONG);
            EmitBytes(BitConverter.GetBytes(global));
        }
    }

    private void Expression()
    {
        ParsePrecedence(Precedence.PREC_ASSIGNMENT);
    }

    private void VarDeclaration()
    {
        int global = ParseVariable("Expect variable name.");

        if (Match(TokenType.TOKEN_EQUAL))
            Expression();
        else
            EmitByte((byte)OpCode.OP_NIL);

        Consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after variable declaration.");

        DefineVariable(global);
    }

    private void ExpressionStatement()
    {
        Expression();
        Consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after expression.");
        EmitByte((byte)OpCode.OP_POP);
    }

    private void PrintStatement()
    {
        Expression();
        Consume(TokenType.TOKEN_SEMICOLON, "Expect ';' after value.");
        EmitByte((byte)OpCode.OP_PRINT);
    }

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

    private void Declaration()
    {
        if (Match(TokenType.TOKEN_VAR))
            VarDeclaration();
        else
            Statement();

        if (_parser.PanicMode)
            Synchronize();
    }

    private void Statement()
    {
        if (Match(TokenType.TOKEN_PRINT))
            PrintStatement();
        else
            ExpressionStatement();
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
            
        }
        else
        {
            Console.Error.WriteLine($" at {token.Lexeme}");
        }

        Console.Error.WriteLine($": {message}");
        _parser.HadError = true;
    }
}
