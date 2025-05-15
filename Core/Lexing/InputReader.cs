// InputReader.cs - Reads input file character by character
namespace Transpiler.Core.Lexing
{
    public class InputReader
    {
        private readonly StreamReader _reader;
        private int _currentLine = 1;
        private int _currentColumn = 0;
        private char _currentChar;
        private bool _isEOF = false;

        public int CurrentLine => _currentLine;
        public int CurrentColumn => _currentColumn;
        public bool IsEOF => _isEOF;

        public InputReader(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Input file not found: {filePath}");
            }

            _reader = new StreamReader(filePath);
            Advance(); // Read the first character
        }

        public char CurrentChar => _currentChar;

        public char Advance()
        {
            int nextChar = _reader.Read();
            
            if (nextChar == -1)
            {
                _isEOF = true;
                _currentChar = '\0';
                return _currentChar;
            }

            _currentChar = (char)nextChar;
            
            // Update line and column counters
            if (_currentChar == '\n')
            {
                _currentLine++;
                _currentColumn = 0;
            }
            else
            {
                _currentColumn++;
            }

            return _currentChar;
        }

        public char Peek()
        {
            if (_isEOF)
            {
                return '\0';
            }

            int nextChar = _reader.Peek();
            return nextChar == -1 ? '\0' : (char)nextChar;
        }

        public void Close()
        {
            _reader.Close();
        }
    }
} 