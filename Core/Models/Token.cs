// Token.cs - Represents a lexical token in the C++ source code
namespace Transpiler.Core.Models
{
    public enum TokenType
    {
        KeywordClass,
        KeywordPublic,
        KeywordPrivate,
        KeywordProtected,
        KeywordConst,
        KeywordStatic,
        KeywordVirtual,
        KeywordOverride,
        KeywordVoid,
        KeywordInt,
        KeywordDouble,
        KeywordBool,
        KeywordString,
        KeywordStdVector,
        KeywordStdList,
        KeywordStdMap,
        KeywordStdSet,
        KeywordStd,
        Unknown,
        Identifier,     // variable names, class names, etc.
        Symbol,         // {, }, (, ), ;, :, etc.
        Operator,       // =, ==, +, -, etc.
        StringLiteral,  // "string"
        NumberLiteral,  // 123, 3.14
        LineComment,        // // comment
        BlockComment,       // /* comment */
        Whitespace,     // spaces, tabs, newlines
        EndOfFile       // end of file marker
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Lexeme { get; }
        public int Line { get; }
        public int Column { get; }

        public Token(TokenType type, string lexeme, int line, int column)
        {
            Type = type;
            Lexeme = lexeme;
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            return $"[{Type}] '{Lexeme}' at Line {Line}, Column {Column}";
        }
    }
} 