// Scanner.cs - Converts characters into tokens
using System.Collections.Generic;
using System.Text;
using Transpiler.Core.Models;

namespace Transpiler.Core.Lexing
{
    public class Scanner
    {
        private readonly InputReader _reader;
        private readonly HashSet<string> _keywords = new HashSet<string>
        {
            "class", "public", "private", "protected", 
            "const", "static", "void", "int", "double", "bool", "string",
            "operator", "override", "std::vector", "std::list", "std::map", "std::set"
        };

        private readonly HashSet<char> _symbolChars = new HashSet<char>
        {
            '{', '}', '(', ')', '[', ']', ';', ':', ',', '.', '<', '>'
        };

        private readonly HashSet<char> _operatorChars = new HashSet<char>
        {
            '+', '-', '*', '/', '=', '!', '&', '|', '~', '^'
        };

        public Scanner(InputReader reader)
        {
            _reader = reader;
        }

        public Token GetNextToken()
        {
            // Skip whitespace
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
            char current = _reader.CurrentChar;

            // Check for comments
            if (current == '/' && _reader.Peek() == '/')
            {
                return ScanLineComment(line, column);
            }
            
            if (current == '/' && _reader.Peek() == '*')
            {
                return ScanBlockComment(line, column);
            }

            // Check for identifiers and keywords
            if (char.IsLetter(current) || current == '_')
            {
                return ScanIdentifierOrKeyword(line, column);
            }
            
            // Check for numbers
            if (char.IsDigit(current))
            {
                return ScanNumber(line, column);
            }
            
            // Check for strings
            if (current == '"')
            {
                return ScanString(line, column);
            }
            
            // Check for symbols
            if (_symbolChars.Contains(current))
            {
                return ScanSymbol(line, column);
            }
            
            // Check for operators
            if (_operatorChars.Contains(current))
            {
                return ScanOperator(line, column);
            }
            
            // Unrecognized character
            string unrecognized = current.ToString();
            _reader.Advance();
            return new Token(TokenType.Symbol, unrecognized, line, column);
        }

        private Token ScanLineComment(int line, int column)
        {
            _reader.Advance(); // Skip first /
            _reader.Advance(); // Skip second /
            
            StringBuilder comment = new StringBuilder("//");
            
            // Read until end of line
            while (!_reader.IsEOF && _reader.CurrentChar != '\n')
            {
                comment.Append(_reader.CurrentChar);
                _reader.Advance();
            }
            
            return new Token(TokenType.Comment, comment.ToString(), line, column);
        }

        private Token ScanBlockComment(int line, int column)
        {
            _reader.Advance(); // Skip /
            _reader.Advance(); // Skip *
            
            StringBuilder comment = new StringBuilder("/*");
            
            // Read until */
            while (!_reader.IsEOF && !(_reader.CurrentChar == '*' && _reader.Peek() == '/'))
            {
                comment.Append(_reader.CurrentChar);
                _reader.Advance();
            }
            
            if (!_reader.IsEOF)
            {
                comment.Append("*/");
                _reader.Advance(); // Skip *
                _reader.Advance(); // Skip /
            }
            
            return new Token(TokenType.Comment, comment.ToString(), line, column);
        }

        private Token ScanIdentifierOrKeyword(int line, int column)
        {
            StringBuilder lexeme = new StringBuilder();
            
            while (!_reader.IsEOF && (char.IsLetterOrDigit(_reader.CurrentChar) || _reader.CurrentChar == '_'))
            {
                lexeme.Append(_reader.CurrentChar);
                _reader.Advance();
            }
            
            string identifier = lexeme.ToString();
            
            // Check if it's a keyword
            if (_keywords.Contains(identifier))
            {
                return new Token(TokenType.Keyword, identifier, line, column);
            }
            
            return new Token(TokenType.Identifier, identifier, line, column);
        }

        private Token ScanNumber(int line, int column)
        {
            StringBuilder number = new StringBuilder();
            bool hasDecimalPoint = false;
            
            while (!_reader.IsEOF && (char.IsDigit(_reader.CurrentChar) || (_reader.CurrentChar == '.' && !hasDecimalPoint)))
            {
                if (_reader.CurrentChar == '.')
                {
                    hasDecimalPoint = true;
                }
                
                number.Append(_reader.CurrentChar);
                _reader.Advance();
            }
            
            return new Token(TokenType.NumberLiteral, number.ToString(), line, column);
        }

        private Token ScanString(int line, int column)
        {
            StringBuilder str = new StringBuilder();
            str.Append(_reader.CurrentChar); // Add opening quote
            _reader.Advance();
            
            while (!_reader.IsEOF && _reader.CurrentChar != '"')
            {
                // Handle escape sequences
                if (_reader.CurrentChar == '\\' && _reader.Peek() == '"')
                {
                    str.Append('\\');
                    _reader.Advance();
                }
                
                str.Append(_reader.CurrentChar);
                _reader.Advance();
            }
            
            // Add closing quote
            if (!_reader.IsEOF && _reader.CurrentChar == '"')
            {
                str.Append(_reader.CurrentChar);
                _reader.Advance();
            }
            
            return new Token(TokenType.StringLiteral, str.ToString(), line, column);
        }

        private Token ScanSymbol(int line, int column)
        {
            char symbol = _reader.CurrentChar;
            _reader.Advance();
            return new Token(TokenType.Symbol, symbol.ToString(), line, column);
        }

        private Token ScanOperator(int line, int column)
        {
            StringBuilder op = new StringBuilder();
            char current = _reader.CurrentChar;
            op.Append(current);
            _reader.Advance();
            
            // Handle multi-character operators (==, !=, >=, <=, etc.)
            if (_operatorChars.Contains(_reader.CurrentChar) || _reader.CurrentChar == '=')
            {
                char next = _reader.CurrentChar;
                
                // Only certain combinations are valid
                if ((current == '=' && next == '=') ||
                    (current == '!' && next == '=') ||
                    (current == '+' && next == '=') ||
                    (current == '-' && next == '=') ||
                    (current == '*' && next == '=') ||
                    (current == '/' && next == '=') ||
                    (current == '&' && next == '&') ||
                    (current == '|' && next == '|') ||
                    (current == '+' && next == '+') ||
                    (current == '-' && next == '-'))
                {
                    op.Append(next);
                    _reader.Advance();
                }
            }
            
            return new Token(TokenType.Operator, op.ToString(), line, column);
        }
    }
} 