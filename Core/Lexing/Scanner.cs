// Scanner.cs - Converts characters into tokens
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Transpiler.Core.Models;

namespace Transpiler.Core.Lexing
{
    public class Scanner
    {
        private readonly InputReader _reader;
        private readonly HashSet<char> _symbolChars = new HashSet<char>
        {
            '{', '}', '(', ')', '[', ']', ';', ':', ',', '.', '<', '>', '~', '#', '=', '&', '*', '+', '-'
        };

        public Scanner(InputReader reader)
        {
            _reader = reader;
        }

        public void Mark()
        {
            _reader.Mark();
        }

        public void Commit()
        {
            _reader.Commit();
        }
        
        public void Reset()
        {
            _reader.Reset();
        }

        public Token GetNextToken()
        {
            while (!_reader.IsEOF && char.IsWhiteSpace(_reader.CurrentChar))
            {
                _reader.Advance();
            }

            // EOF
            if (_reader.IsEOF)
            {
                return new Token(TokenType.EndOfFile, "", _reader.CurrentLine, _reader.CurrentColumn);
            }

            // Save current position for token location
            int line = _reader.CurrentLine;
            int column = _reader.CurrentColumn;

            // Try to parse each token type in sequence
            Token token;

            if (TryParseLineComment(line, column, out token) ||
                TryParseBlockComment(line, column, out token) ||
                TryParseNumber(line, column, out token) ||
                TryParseIdentifierOrKeyword(line, column, out token) ||
                TryParseString(line, column, out token) ||
                TryParseSymbol(line, column, out token))
            {
                if (token == null)
                {
                    return new Token(TokenType.EndOfFile, "", _reader.CurrentLine, _reader.CurrentColumn);
                }
                return token;
            }

            // If no token was parsed, return an unknown token or throw an error
            return new Token(TokenType.Unknown, _reader.CurrentChar.ToString(), line, column);
        }

        private bool TryParseLineComment(int line, int column, out Token token)
        {

            token = null;

            if (_reader.IsEOF || _reader.CurrentChar != '/') return false;

            _reader.Mark();
            _reader.Advance();

            if (_reader.IsEOF || _reader.CurrentChar != '/')
            {
                _reader.Reset();
                return false;
            }

            _reader.Commit();

            StringBuilder comment = new StringBuilder("//");

            while (!_reader.IsEOF && _reader.CurrentChar != '\n')
            {
                comment.Append(_reader.CurrentChar);
                _reader.Advance();
            }

            token = new Token(TokenType.LineComment, comment.ToString(), line, column);

            return true;
        }

        private bool TryParseBlockComment(int line, int column, out Token token)
        {
            token = null;

            if (_reader.IsEOF || _reader.CurrentChar != '/') return false;

            _reader.Mark();
            _reader.Advance();

            if (_reader.IsEOF || _reader.CurrentChar != '*')
            {
                _reader.Reset();
                return false;
            }

            _reader.Commit();

            StringBuilder comment = new StringBuilder("/*");
            bool foundEnd = false;

            while (!_reader.IsEOF)
            {
                if (_reader.CurrentChar == '*')
                {
                    comment.Append('*');
                    _reader.Advance();

                    if (!_reader.IsEOF && _reader.CurrentChar == '/')
                    {
                        comment.Append('/');
                        _reader.Advance();
                        foundEnd = true;
                        break;
                    }
                }
                else
                {
                    comment.Append(_reader.CurrentChar);
                    _reader.Advance();
                }
            }

            if (!foundEnd)
            {
                // Handle unclosed block comment
                throw new SyntaxErrorException(_reader.CurrentLine, _reader.CurrentColumn,
                    "Unclosed block comment");
            }

            token = new Token(TokenType.BlockComment, comment.ToString(), line, column);

            return true;
        }

        private bool TryParseIdentifierOrKeyword(int line, int column, out Token token)
        {
            token = null;

            // Check if the current character is a letter or underscore
            if (_reader.IsEOF || !(char.IsLetter(_reader.CurrentChar) || _reader.CurrentChar == '_'))
                return false;

            StringBuilder lexeme = new StringBuilder();

            // Append the current character to the lexeme
            while (!_reader.IsEOF && (char.IsLetterOrDigit(_reader.CurrentChar) || _reader.CurrentChar == '_' || _reader.CurrentChar == '='))
            {
                lexeme.Append(_reader.CurrentChar);
                _reader.Advance();
            }

            string identifier = lexeme.ToString();

            // Check if it's a valid keyword, if not, treat it as an identifier
            TokenType tokenType = KeywordToTokenType(identifier);

            token = new Token(tokenType, identifier, line, column);
            return true;
        }

        private bool TryParseNumber(int line, int column, out Token token)
        {
            token = null;

            if (_reader.IsEOF || !char.IsDigit(_reader.CurrentChar))
                return false;

            StringBuilder number = new StringBuilder();
            bool hasDecimalPoint = false;

            while (!_reader.IsEOF && (char.IsDigit(_reader.CurrentChar) || _reader.CurrentChar == '.'))
            {
                if (_reader.CurrentChar == '.')
                {
                    if (hasDecimalPoint)
                        break; // Only allow one decimal point

                    // Mark position before consuming decimal point
                    _reader.Mark();
                    number.Append(_reader.CurrentChar);
                    _reader.Advance();

                    // If the next character is not a digit, backtrack and end the number
                    if (_reader.IsEOF || !char.IsDigit(_reader.CurrentChar))
                    {
                        _reader.Reset();
                        break;
                    }

                    // It's a valid decimal point in a number
                    _reader.Commit();
                    hasDecimalPoint = true;
                }
                else
                {
                    number.Append(_reader.CurrentChar);
                    _reader.Advance();
                }
            }

            token = new Token(TokenType.NumberLiteral, number.ToString(), line, column);
            return true;
        }

        private bool TryParseString(int line, int column, out Token token)
        {
            token = null;

            if (_reader.IsEOF || _reader.CurrentChar != '"')
                return false;

            StringBuilder str = new StringBuilder();
            str.Append(_reader.CurrentChar); // Add opening quote
            _reader.Advance();

            bool escaped = false;

            while (!_reader.IsEOF)
            {
                if (escaped)
                {
                    // Handle escaped character
                    str.Append(_reader.CurrentChar);
                    _reader.Advance();
                    escaped = false;
                }
                else if (_reader.CurrentChar == '\\')
                {
                    // Start of escape sequence
                    str.Append(_reader.CurrentChar);
                    _reader.Advance();
                    escaped = true;
                }
                else if (_reader.CurrentChar == '"')
                {
                    // End of string
                    str.Append(_reader.CurrentChar);
                    _reader.Advance();
                    token = new Token(TokenType.StringLiteral, str.ToString(), line, column);
                    return true;
                }
                else
                {
                    // Regular character
                    str.Append(_reader.CurrentChar);
                    _reader.Advance();
                }
            }

            // If we get here, the string wasn't terminated properly
            throw new SyntaxErrorException(line, column, "Unclosed string literal");
        }

        private bool TryParseSymbol(int line, int column, out Token token)
        {
            token = null;

            if (_reader.IsEOF || !_symbolChars.Contains(_reader.CurrentChar))
                return false;

            char symbol = _reader.CurrentChar;
            _reader.Advance();

            token = new Token(TokenType.Symbol, symbol.ToString(), line, column);
            return true;
        }
        
        private TokenType KeywordToTokenType(string keyword)
        {
            return keyword switch
            {
                "class" => TokenType.KeywordClass,
                "public" => TokenType.KeywordPublic,
                "private" => TokenType.KeywordPrivate,
                "protected" => TokenType.KeywordProtected,
                "const" => TokenType.KeywordConst,
                "static" => TokenType.KeywordStatic,
                "void" => TokenType.KeywordVoid,
                "int" => TokenType.KeywordInt,
                "double" => TokenType.KeywordDouble,
                "bool" => TokenType.KeywordBool,
                "string" => TokenType.KeywordString,
                "override" => TokenType.KeywordOverride,
                "virtual" => TokenType.KeywordVirtual,
                "vector" => TokenType.KeywordStdVector,
                "list" => TokenType.KeywordStdList,
                "map" => TokenType.KeywordStdMap,
                "set" => TokenType.KeywordStdSet,
                "std" => TokenType.KeywordStd,
                _ => TokenType.Identifier
            };
        }
    }
} 