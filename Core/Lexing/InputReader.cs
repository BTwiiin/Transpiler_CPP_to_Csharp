public class InputReader
{
    private readonly StreamReader _reader;
    private readonly char[] _buffer;
    private int _bufferSize;
    private int _position = 0;
    private int _currentLine = 1;
    private int _currentColumn = 0;
    private bool _isEOF = false;
    
    // Track previous positions for backtracking
    private class Position
    {
        public int BufferPos;
        public int Line;
        public int Column;
    }
    
    private readonly Stack<Position> _positions = new Stack<Position>();
    
    public InputReader(string filePath, int bufferSize = 4096)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Input file not found: {filePath}");
        }

        _reader = new StreamReader(filePath);
        _buffer = new char[bufferSize];
        _bufferSize = 0;
        
        // Initial buffer fill
        FillBuffer();
    }
    
    public char CurrentChar => _isEOF ? '\0' : _buffer[_position];
    public int CurrentLine => _currentLine;
    public int CurrentColumn => _currentColumn;
    public bool IsEOF => _isEOF;
    
    private void FillBuffer()
    {
        if (_position < _bufferSize) return; // Buffer still has data
        
        _bufferSize = _reader.Read(_buffer, 0, _buffer.Length);
        _position = 0;
        
        if (_bufferSize == 0)
        {
            _isEOF = true;
        }
    }
    
    public void Advance()
    {
        if (_isEOF) return;
        
        char current = CurrentChar;

        
        _position++;
        if (_position >= _bufferSize)
        {
            FillBuffer();
        }
        
        // Update line and column tracking
        if (current == '\n')
        {
            _currentLine++;
            _currentColumn = 0;
        }
        else
        {
            _currentColumn++;
        }
    }

    public int GetMarkDepth()
    {
        return _positions.Count;
    }
    
    // Mark current position to allow backtracking
    public void Mark()
    {
        _positions.Push(new Position
        {
            BufferPos = _position,
            Line = _currentLine,
            Column = _currentColumn
        });
    }
    
    // Backtrack to last marked position
    public void Reset()
    {
        if (_positions.Count == 0)
            throw new InvalidOperationException("No marked position to return to");
        
        var pos = _positions.Pop();
        
        // This assumes we don't need to go back further than the current buffer
        // For more complex backtracking, you'd need to track file positions
        _position = pos.BufferPos;
        _currentLine = pos.Line;
        _currentColumn = pos.Column;
    }
    
    // Discard the last marked position when we commit to a token
    public void Commit()
    {
        if (_positions.Count > 0)
        {
            _positions.Pop();
        }
    }
}