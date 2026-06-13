#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Neonalig.Core.Editor
{
    public static class CSharpNameExtensions
    {
        private static readonly Dictionary<Type, string> _aliases = new()
        {
            [typeof(void)] = "void",
            [typeof(bool)] = "bool",
            [typeof(byte)] = "byte",
            [typeof(sbyte)] = "sbyte",
            [typeof(short)] = "short",
            [typeof(ushort)] = "ushort",
            [typeof(int)] = "int",
            [typeof(uint)] = "uint",
            [typeof(long)] = "long",
            [typeof(ulong)] = "ulong",
            [typeof(float)] = "float",
            [typeof(double)] = "double",
            [typeof(decimal)] = "decimal",
            [typeof(char)] = "char",
            [typeof(string)] = "string",
            [typeof(object)] = "object",
            [typeof(nint)] = "nint",
            [typeof(nuint)] = "nuint",
        };

        private static readonly HashSet<string> _keywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in",
            "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator",
            "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte",
            "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while",
            // contextual keywords (safe to escape too)
            "add", "alias", "and", "ascending", "async", "await", "by", "descending", "dynamic", "equals", "from",
            "get", "global", "group", "init", "into", "join", "let", "managed", "nameof", "nint", "nuint", "not",
            "on", "or", "orderby", "partial", "record", "remove", "required", "select", "set", "unmanaged",
            "value", "var", "when", "where", "with", "yield"
        };

        private static readonly HashSet<char> _illegalTypeFileNameCharacters = new()
        {
            '<',
            '>',
            '?',
            ' ',
            ',',
            ':',
        };

        /// <summary>
        /// Returns a C#-style display name for a type (aliases, generics, nullables, tuples, nested types).
        /// </summary>
        public static string CSharpName(this Type type, bool includeNamespace = false)
            => FormatType(type, includeNamespace);

        /// <summary>
        /// Returns a filename-safe C# style type name.
        /// </summary>
        public static string CSharpFileName(this Type type, bool includeNamespace, bool includeGenericParameters = false)
        {
            var fileName = type.CSharpName(includeNamespace);

            if (!includeGenericParameters && type.IsGenericType)
            {
                var genericStartIndex = fileName.IndexOf('<');
                if (genericStartIndex >= 0)
                {
                    fileName = fileName[..genericStartIndex];
                }
            }

            var sanitized = new StringBuilder(fileName.Length);
            var previousWasUnderscore = false;

            foreach (var c in fileName)
            {
                var normalized = _illegalTypeFileNameCharacters.Contains(c) ? '_' : c;
                if (normalized == '_')
                {
                    if (previousWasUnderscore)
                    {
                        continue;
                    }

                    previousWasUnderscore = true;
                    sanitized.Append('_');
                    continue;
                }

                previousWasUnderscore = false;
                sanitized.Append(normalized);
            }

            return sanitized.ToString().Trim('_');
        }

        /// <summary>
        /// Returns a C#-style display name for a method/constructor (incl. generic args + parameters with ref/out/in).
        /// </summary>
        public static string CSharpName(this MethodBase method, bool includeNamespaceForDeclaringType = false, bool includeParameterNames = false)
        {
            var sb = new StringBuilder();

            // Declaring type prefix (optional, but usually what VS shows in node titles/tooltips)
            if (method.DeclaringType is not null)
            {
                sb.Append(FormatType(method.DeclaringType, includeNamespaceForDeclaringType));
                sb.Append('.');
            }

            sb.Append(FormatMethodName(method));

            // Generic method args
            if (method is MethodInfo { IsGenericMethod: true } mi)
            {
                var args = mi.GetGenericArguments();
                sb.Append('<');
                for (var i = 0; i < args.Length; i++)
                {
                    if (i != 0) sb.Append(", ");
                    sb.Append(FormatType(args[i], includeNamespace: false));
                }
                sb.Append('>');
            }

            // Parameters
            sb.Append('(');
            var ps = method.GetParameters();
            for (var i = 0; i < ps.Length; i++)
            {
                if (i != 0) sb.Append(", ");
                sb.Append(FormatParameter(ps[i], includeNamespaceForDeclaringType, includeParameterNames));
            }
            sb.Append(')');

            return sb.ToString();
        }

        private static string FormatMethodName(MethodBase method)
        {
            // Constructors
            if (method is ConstructorInfo)
            {
                var name = method.DeclaringType?.Name ?? method.Name;
                return EscapeIdentifier(StripArity(name));
            }

            // Operators (optional nicety)
            if (method is MethodInfo { IsSpecialName: true } mi)
            {
                if (_operatorNames.TryGetValue(mi.Name, out var op))
                    return "operator " + op;
            }

            return EscapeIdentifier(method.Name);
        }

        private static string FormatParameter(ParameterInfo p, bool includeNamespace, bool includeParameterName)
        {
            var sb = new StringBuilder();

            // ref/out/in
            if (p.ParameterType.IsByRef)
            {
                if (p.IsOut) sb.Append("out ");
                else if (p.IsIn) sb.Append("in ");
                else sb.Append("ref ");
            }

            var paramType = p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
            sb.Append(FormatType(paramType, includeNamespace));

            if (includeParameterName)
            {
                sb.Append(' ');
                sb.Append(EscapeIdentifier(p.Name ?? $"arg{p.Position}"));
            }

            return sb.ToString();
        }

        private static string FormatType(Type type, bool includeNamespace)
        {
            // Aliases
            if (_aliases.TryGetValue(type, out var alias))
                return alias;

            // Generic parameter (T)
            if (type.IsGenericParameter)
                return EscapeIdentifier(type.Name);

            // ByRef / Pointer (rare in graphs, but good to support)
            if (type.IsByRef)
                return "ref " + FormatType(type.GetElementType()!, includeNamespace);
            if (type.IsPointer)
                return FormatType(type.GetElementType()!, includeNamespace) + "*";

            // Array
            if (type.IsArray)
            {
                var elem = FormatType(type.GetElementType()!, includeNamespace);
                var rank = type.GetArrayRank();
                return rank == 1 ? $"{elem}[]" : $"{elem}[{new string(',', rank - 1)}]";
            }

            // Nullable<T> => T?
            if (IsNullable(type, out var underlyingNullable))
                return FormatType(underlyingNullable, includeNamespace) + "?";

            // ValueTuple => (T1, T2, ...)
            if (IsValueTuple(type, out var tupleArgs))
            {
                var inner = string.Join(", ", tupleArgs.Select(a => FormatType(a, includeNamespace)));
                return $"({inner})";
            }

            // Generic types: Namespace.Outer.Inner<...>
            if (type.IsGenericType)
            {
                // Handle nested generic names properly by walking declaring types.
                return FormatGenericType(type, includeNamespace);
            }

            // Non-generic: Namespace.Outer.Inner
            return FormatNonGenericTypeName(type, includeNamespace);
        }

        private static string FormatNonGenericTypeName(Type type, bool includeNamespace)
        {
            // Nested types: A.B
            if (type is { IsNested: true, DeclaringType: not null })
                return $"{FormatType(type.DeclaringType, includeNamespace)}.{EscapeIdentifier(type.Name)}";

            var name = EscapeIdentifier(type.Name);

            if (!includeNamespace || string.IsNullOrEmpty(type.Namespace))
                return name;

            return $"{type.Namespace}.{name}";
        }

        private static string FormatGenericType(Type type, bool includeNamespace)
        {
            // Split generic args across the whole nested chain, then rebuild names with correct arity at each level.
            var allArgs = type.GetGenericArguments();
            var chain = GetDeclaringTypeChain(type); // outer -> inner
            var sb = new StringBuilder();

            // Namespace prefix only once (on the outermost non-nested type)
            var outer = chain[0];
            if (includeNamespace && !string.IsNullOrEmpty(outer.Namespace))
            {
                sb.Append(outer.Namespace);
                sb.Append('.');
            }

            var argIndex = 0;

            for (var i = 0; i < chain.Count; i++)
            {
                if (i != 0) sb.Append('.');

                var current = chain[i];
                var (baseName, arity) = SplitArity(current.Name);

                sb.Append(EscapeIdentifier(baseName));

                if (arity > 0)
                {
                    sb.Append('<');
                    for (var j = 0; j < arity; j++)
                    {
                        if (j != 0) sb.Append(", ");
                        sb.Append(FormatType(allArgs[argIndex++], includeNamespace: false));
                    }
                    sb.Append('>');
                }
            }

            return sb.ToString();
        }

        private static List<Type> GetDeclaringTypeChain(Type type)
        {
            var stack = new Stack<Type>();
            for (var t = type; t is not null; t = t.DeclaringType)
                stack.Push(t);
            return stack.ToList();
        }

        private static bool IsNullable(Type t, out Type underlying)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                underlying = t.GetGenericArguments()[0];
                return true;
            }
            underlying = null!;
            return false;
        }

        private static bool IsValueTuple(Type t, out Type[] args)
        {
            if (!t.IsGenericType)
            {
                args = Array.Empty<Type>();
                return false;
            }

            var def = t.GetGenericTypeDefinition();
            var isTuple =
                def == typeof(ValueTuple<>) ||
                def == typeof(ValueTuple<,>) ||
                def == typeof(ValueTuple<,,>) ||
                def == typeof(ValueTuple<,,,>) ||
                def == typeof(ValueTuple<,,,,>) ||
                def == typeof(ValueTuple<,,,,,>) ||
                def == typeof(ValueTuple<,,,,,,>) ||
                def == typeof(ValueTuple<,,,,,,,>);

            if (!isTuple)
            {
                args = Array.Empty<Type>();
                return false;
            }

            // Flatten the TRest chain if present (8th arg)
            var list = new List<Type>();
            FlattenValueTuple(t, list);
            args = list.ToArray();
            return true;
        }

        private static void FlattenValueTuple(Type tuple, List<Type> acc)
        {
            var a = tuple.GetGenericArguments();
            if (a.Length == 8 && IsValueTuple(a[7], out _))
            {
                for (var i = 0; i < 7; i++) acc.Add(a[i]);
                FlattenValueTuple(a[7], acc);
            }
            else
            {
                acc.AddRange(a);
            }
        }

        private static string EscapeIdentifier(string s)
            => (s.Length == 0 || _keywords.Contains(s)) ? "@" + s : s;

        private static string StripArity(string name)
            => SplitArity(name).baseName;

        private static (string baseName, int arity) SplitArity(string name)
        {
            var tick = name.IndexOf('`');
            if (tick < 0) return (name, 0);
            if (int.TryParse(name.AsSpan(tick + 1), out var arity))
                return (name[..tick], arity);
            return (name[..tick], 0);
        }

        private static readonly Dictionary<string, string> _operatorNames = new(StringComparer.Ordinal)
        {
            ["op_Addition"] = "+",
            ["op_Subtraction"] = "-",
            ["op_Multiply"] = "*",
            ["op_Division"] = "/",
            ["op_Modulus"] = "%",
            ["op_ExclusiveOr"] = "^",
            ["op_BitwiseAnd"] = "&",
            ["op_BitwiseOr"] = "|",
            ["op_LogicalAnd"] = "&&",
            ["op_LogicalOr"] = "||",
            ["op_LeftShift"] = "<<",
            ["op_RightShift"] = ">>",
            ["op_Equality"] = "==",
            ["op_Inequality"] = "!=",
            ["op_GreaterThan"] = ">",
            ["op_LessThan"] = "<",
            ["op_GreaterThanOrEqual"] = ">=",
            ["op_LessThanOrEqual"] = "<=",
            ["op_UnaryNegation"] = "-",
            ["op_UnaryPlus"] = "+",
            ["op_Increment"] = "++",
            ["op_Decrement"] = "--",
            ["op_OnesComplement"] = "~",
            ["op_True"] = "true",
            ["op_False"] = "false",
            ["op_Implicit"] = "implicit",
            ["op_Explicit"] = "explicit",
        };
    }
}
