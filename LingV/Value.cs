using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LingV;

public enum ValueType
{
    VAL_BOOL,
    VAL_NIL,
    VAL_NUMBER,
    VAL_STRING,
    VAL_OBJ
}

public struct StringValue(string value)
{
    public string str = value;
}

public struct ObjValue(object value)
{
    public object obj = value;
}

public struct TestValue(int value)
{
    public int integer = value;
}

[StructLayout(LayoutKind.Explicit)]
public struct Value
{
    [FieldOffset(0)] public ValueType type;
    [FieldOffset(4)] public bool boolean;
    [FieldOffset(4)] public double number;
    [FieldOffset(16)] public string str;
    //[FieldOffset(16)] public object obj;

    public static Value BoolVal(bool value)
    {
        return new() { type = ValueType.VAL_BOOL, boolean = value };
    }

    public static Value NilVal()
    {
        return new() { type = ValueType.VAL_NIL, number = 0 };
    }

    public static Value NumberVal(double value)
    {
        return new() { type = ValueType.VAL_NUMBER, number = value };
    }

    public static Value StringVal(string value)
    {
        return new() { type = ValueType.VAL_STRING, str = value };
    }

    //public static Value ObjVal(object value)
    //{
    //    return new() { type = ValueType.VAL_OBJ, obj = new(value) };
    //}

    public readonly bool AsBool()
    {
        return boolean;
    }

    public readonly double AsNumber()
    {
        return number;
    }

    public readonly string AsString()
    {
        return str;
    }

    //public readonly object AsObj()
    //{
    //    return obj;
    //}

    public readonly bool IsBool()
    {
        return type == ValueType.VAL_BOOL;
    }

    public readonly bool IsNil()
    {
        return type == ValueType.VAL_NIL;
    }

    public readonly bool IsNumber()
    {
        return type == ValueType.VAL_NUMBER;
    }

    public readonly bool IsString()
    {
        return type == ValueType.VAL_STRING;
    }

    public readonly bool IsObj()
    {
        return type == ValueType.VAL_OBJ;
    }

    public readonly void PrintValue()
    {
        switch (type)
        {
            case ValueType.VAL_BOOL:
                Console.WriteLine($"{AsBool():G}");
                break;
            case ValueType.VAL_NIL:
                Console.WriteLine("nil");
                break;
            case ValueType.VAL_NUMBER:
                Console.WriteLine($"{AsNumber():G}");
                break;
            case ValueType.VAL_STRING:
                Console.WriteLine(AsString());
                break;
            //case ValueType.VAL_OBJ:
            //    Console.WriteLine($"{AsObj()}");
            //    break;
        }
    }

    public override readonly string ToString()
    {
        switch (type)
        {
            case ValueType.VAL_BOOL:
                return $"{AsBool():G}";
            case ValueType.VAL_NIL:
                return "nil";
            case ValueType.VAL_NUMBER:
                return $"{AsNumber():G}";
            case ValueType.VAL_STRING:
                return str;
            //case ValueType.VAL_OBJ:
            //    return obj.ToString();
            default:
                return "nil";
        }
    }

    public static bool ValuesEqual(Value a, Value b)
    {
        if (a.type != b.type)
            return false;

        switch(a.type)
        {
            case ValueType.VAL_BOOL: return a.AsBool() == b.AsBool();
            case ValueType.VAL_NIL: return true;
            case ValueType.VAL_NUMBER: return a.AsNumber() == b.AsNumber();
            case ValueType.VAL_STRING: return a.AsString() == b.AsString();
            //case ValueType.VAL_OBJ: return a.AsObj() == b.AsObj();
            default: return false;
        }
    }
}

public struct ValueList()
{
    public List<Value> Values = [];
    
    public readonly void Write(Value value)
    {
        Values.Add(value);
    }
}