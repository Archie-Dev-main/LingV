using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LingV;

public enum TokenType
{
    // Single-character tokens.
    TOKEN_LEFT_PAREN, TOKEN_RIGHT_PAREN,
    TOKEN_LEFT_BRACE, TOKEN_RIGHT_BRACE,
    TOKEN_COMMA, TOKEN_DOT, TOKEN_MINUS, TOKEN_PLUS,
    TOKEN_SEMICOLON, TOKEN_SLASH, TOKEN_STAR,
    // One or two character tokens.
    TOKEN_BANG, TOKEN_BANG_EQUAL,
    TOKEN_EQUAL, TOKEN_EQUAL_EQUAL,
    TOKEN_GREATER, TOKEN_GREATER_EQUAL,
    TOKEN_LESS, TOKEN_LESS_EQUAL,
    // Literals.
    TOKEN_IDENTIFIER, TOKEN_STRING, TOKEN_NUMBER,
    // Keywords.
    TOKEN_AND, TOKEN_BREAK, TOKEN_CLASS, TOKEN_CONTINUE, TOKEN_ELSE, TOKEN_FALSE,
    TOKEN_FOR, TOKEN_FUN, TOKEN_IF, TOKEN_NIL, TOKEN_OR,
    TOKEN_PRINT, TOKEN_RETURN, TOKEN_SUPER, TOKEN_THIS,
    TOKEN_TRUE, TOKEN_VAR, TOKEN_WHILE,

    TOKEN_ERROR, TOKEN_EOF
}

public struct Token(TokenType type, string lexeme, int line)
{
    public TokenType Type = type;
    public string Lexeme = lexeme;
    public int Line = line;
}

public class ReservedWords
{
    public static ReservedWords Instance { get; private set; } = new();

    public readonly Dictionary<string, TokenType> Keywords = [];

    private ReservedWords()
    {
        Keywords["and"] = TokenType.TOKEN_AND;
        Keywords["break"] = TokenType.TOKEN_BREAK;
        Keywords["class"] = TokenType.TOKEN_CLASS;
        Keywords["continue"] = TokenType.TOKEN_CONTINUE;
        Keywords["else"] = TokenType.TOKEN_ELSE;
        Keywords["false"] = TokenType.TOKEN_FALSE;
        Keywords["for"] = TokenType.TOKEN_FOR;
        Keywords["fun"] = TokenType.TOKEN_FUN;
        Keywords["if"] = TokenType.TOKEN_IF;
        Keywords["nil"] = TokenType.TOKEN_NIL;
        Keywords["or"] = TokenType.TOKEN_OR;
        Keywords["print"] = TokenType.TOKEN_PRINT;
        Keywords["return"] = TokenType.TOKEN_RETURN;
        Keywords["super"] = TokenType.TOKEN_SUPER;
        Keywords["this"] = TokenType.TOKEN_THIS;
        Keywords["true"] = TokenType.TOKEN_TRUE;
        Keywords["var"] = TokenType.TOKEN_VAR;
        Keywords["while"] = TokenType.TOKEN_WHILE;
    }
}

public class Scanner(string source)
{
    private readonly string _source = source;
    private int _start = 0;
    private int _current = 0;
    private int _line = 1;

    private static bool IsAlphaNumeric(char c)
    {
        return IsAlpha(c) || IsDigit(c);
    }

    private static bool IsAlpha(char c)
    {
        return (c >= 'a' && c <= 'z') ||
               (c >= 'A' && c <= 'Z') ||
               c == '_';
    }

    private static bool IsDigit(char c)
    {
        return c >= '0' && c <= '9';
    }

    public Token ScanToken()
    {
        SkipWhiteSpace();
        _start = _current;

        if (IsAtEnd())
            return MakeToken(TokenType.TOKEN_EOF);

        char c = Advance();

        if (IsDigit(c))
            return GetNumber();

        if (IsAlpha(c))
            return GetIdentifier();

        switch (c)
        {
            case '(': return MakeToken(TokenType.TOKEN_LEFT_PAREN);
            case ')': return MakeToken(TokenType.TOKEN_RIGHT_PAREN);
            case '{': return MakeToken(TokenType.TOKEN_LEFT_BRACE);
            case '}': return MakeToken(TokenType.TOKEN_RIGHT_BRACE);
            case ';': return MakeToken(TokenType.TOKEN_SEMICOLON);
            case ',': return MakeToken(TokenType.TOKEN_COMMA);
            case '.': return MakeToken(TokenType.TOKEN_DOT);
            case '-': return MakeToken(TokenType.TOKEN_MINUS);
            case '+': return MakeToken(TokenType.TOKEN_PLUS);
            case '/': return MakeToken(TokenType.TOKEN_SLASH);
            case '*': return MakeToken(TokenType.TOKEN_STAR);
            case '!':
                return MakeToken(Match('=') ? TokenType.TOKEN_BANG_EQUAL : TokenType.TOKEN_BANG);
            case '=':
                return MakeToken(Match('=') ? TokenType.TOKEN_EQUAL_EQUAL : TokenType.TOKEN_EQUAL);
            case '<':
                return MakeToken(Match('=') ? TokenType.TOKEN_LESS_EQUAL : TokenType.TOKEN_LESS);
            case '>':
                return MakeToken(Match('=') ? TokenType.TOKEN_GREATER_EQUAL : TokenType.TOKEN_GREATER);
            case '"': return GetString();
        }

        return ErrorToken("Unexpected character.");
    }

    private bool IsAtEnd()
    {
        return _source[_current] == '\0';
    }

    private char Advance()
    {
        //Console.WriteLine($"Current char {_source[_current]}");

        //if (_source[_current] == '2')
        //    Console.WriteLine("skipped");

        return _source[_current++];
    }

    private char Peek()
    {
        return _source[_current];
    }

    private char PeekNext()
    {
        if (IsAtEnd())
            return '\0';

        return _source[_current + 1];
    }

    private bool Match(char expected)
    {
        if (IsAtEnd())
            return false;

        if (Peek() != expected)
            return false;

        Advance();
        return true;
    }

    private Token MakeToken(TokenType type)
    {
        return new(type, _source[_start.._current], _line);
    }

    private Token ErrorToken(string message)
    {
        return new(TokenType.TOKEN_ERROR, message, _line);
    }

    private void SkipWhiteSpace()
    {
        for (; ;)
        {
            char c = Peek();

            switch (c)
            {
                case ' ':
                case '\r':
                case '\t':
                    Advance();
                    break;
                case '\n':
                    _line++;
                    Advance();
                    break;
                case '/':
                    if (PeekNext() == '/')
                    {
                        while (Peek() != '\n' && !IsAtEnd())
                            Advance();
                    }
                    else
                        return;

                    break;
                default:
                    return;
            }
        }
    }

    private Token GetIdentifier()
    {
        while (IsAlphaNumeric(Peek()))
            Advance();

        string text = _source[_start.._current];
        TokenType type;

        if (ReservedWords.Instance.Keywords.TryGetValue(text, out var value))
        {
            type = value;
        }
        else
        {
            type = TokenType.TOKEN_IDENTIFIER;
        }

        return new(type, text, _line);
    }

    private Token GetNumber()
    {
        while (IsDigit(Peek()))
            Advance();

        if (Peek() == '.' && IsDigit(PeekNext()))
        {
            Advance();

            while (IsDigit(Peek()))
                Advance();
        }

        return MakeToken(TokenType.TOKEN_NUMBER);
    }

    private Token GetString()
    {
        while (Peek() != '"' && !IsAtEnd())
        {
            if (Peek() == '\n')
                _line++;

            Advance();
        }

        if (IsAtEnd())
        {
            return ErrorToken("Unterminated string.");
        }

        Advance();

        return new(TokenType.TOKEN_STRING, _source[(_start+1)..(_current-1)], _line);
    }

    //public int GetLine()
    //{
    //    return _line;
    //}

    //public void SetLine(int line)
    //{
    //    _line = line;
    //}
}
