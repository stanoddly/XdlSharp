using System.Text.RegularExpressions;

namespace XdlSharp.Generator;

// A C type as clang prints it in qualType, before typedefs are resolved.
internal abstract record CType;

// A primitive, typedef or tag name; IsTag means it was spelled with struct, union or enum.
internal sealed record CNamedType(string Name, bool IsConst, bool IsTag) : CType;

internal sealed record CPointerType(CType Pointee) : CType;

internal sealed record CArrayType(CType Element, int Length) : CType;

internal sealed record CFunctionType(CType Return, IReadOnlyList<CType> Parameters, bool IsVariadic) : CType;

internal static partial class CTypeParser
{
    public static CType Parse(string text)
    {
        List<string> tokens = Tokenize(text);
        int position = 0;
        CType type = ParseType(tokens, ref position, text);
        if (position != tokens.Count)
        {
            throw new FormatException($"Unexpected '{tokens[position]}' in the C type '{text}'.");
        }

        return type;
    }

    // "const char *(SDL_PropertiesID, const char *)" is the type of a function; its return type is the text before the
    // parameter list, which is the last balanced group.
    public static string ReturnTypeOfFunction(string functionType)
    {
        int depth = 0;
        for (int index = functionType.Length - 1; index >= 0; index--)
        {
            char character = functionType[index];
            if (character == ')')
            {
                depth++;
            }
            else if (character == '(')
            {
                depth--;
                if (depth == 0)
                {
                    return functionType[..index].Trim();
                }
            }
        }

        throw new FormatException($"'{functionType}' is not a function type.");
    }

    private static CType ParseType(List<string> tokens, ref int position, string text)
    {
        bool isConst = false;
        bool isTag = false;
        List<string> words = [];
        while (position < tokens.Count && IsWord(tokens[position]))
        {
            string word = tokens[position++];
            if (word == "const")
            {
                isConst = true;
            }
            else if (word is "volatile" or "restrict" or "__restrict")
            {
            }
            else if (word is "struct" or "union" or "enum")
            {
                isTag = true;
            }
            else
            {
                words.Add(word);
            }
        }

        if (words.Count == 0)
        {
            throw new FormatException($"No type name in the C type '{text}'.");
        }

        CType type = new CNamedType(string.Join(' ', words), isConst, isTag);
        while (position < tokens.Count && tokens[position] == "*")
        {
            position++;
            type = new CPointerType(type);
            SkipQualifiers(tokens, ref position);
        }

        if (position < tokens.Count && tokens[position] == "(")
        {
            position++;
            int pointerCount = 0;
            while (position < tokens.Count && tokens[position] == "*")
            {
                position++;
                pointerCount++;
                SkipQualifiers(tokens, ref position);
            }

            Expect(tokens, ref position, ")", text);
            Expect(tokens, ref position, "(", text);
            List<CType> parameters = [];
            bool isVariadic = false;
            while (tokens[position] != ")")
            {
                if (tokens[position] == "...")
                {
                    isVariadic = true;
                    position++;
                }
                else
                {
                    parameters.Add(ParseType(tokens, ref position, text));
                }

                if (tokens[position] == ",")
                {
                    position++;
                }
            }

            position++;
            if (parameters is [CNamedType { Name: "void" }])
            {
                parameters.Clear();
            }

            type = new CFunctionType(type, parameters, isVariadic);
            for (int index = 0; index < pointerCount; index++)
            {
                type = new CPointerType(type);
            }
        }

        while (position < tokens.Count && tokens[position] == "[")
        {
            position++;
            int length = int.Parse(tokens[position++]);
            Expect(tokens, ref position, "]", text);
            type = new CArrayType(type, length);
        }

        return type;
    }

    private static void SkipQualifiers(List<string> tokens, ref int position)
    {
        while (position < tokens.Count && tokens[position] is "const" or "volatile" or "restrict" or "__restrict")
        {
            position++;
        }
    }

    private static void Expect(List<string> tokens, ref int position, string expected, string text)
    {
        if (position >= tokens.Count || tokens[position] != expected)
        {
            throw new FormatException($"Expected '{expected}' in the C type '{text}'.");
        }

        position++;
    }

    private static bool IsWord(string token)
    {
        return char.IsLetter(token[0]) || token[0] == '_';
    }

    private static List<string> Tokenize(string text)
    {
        List<string> tokens = [];
        foreach (Match match in TokenPattern().Matches(text))
        {
            tokens.Add(match.Value);
        }

        string joined = string.Concat(tokens);
        if (joined.Length != text.Replace(" ", "").Length)
        {
            throw new FormatException($"Unsupported characters in the C type '{text}'.");
        }

        return tokens;
    }

    [GeneratedRegex(@"\.\.\.|[A-Za-z_][A-Za-z_0-9]*|[0-9]+|[*()\[\],]")]
    private static partial Regex TokenPattern();
}
