using System.Runtime.Serialization;
using System.Text.Json.Serialization;

[Serializable]
[JsonSerializable(typeof(SyntaxErrorException))]
public class SyntaxErrorException : Exception
{
    [JsonInclude]
    public int LineNumber { get; }
    [JsonInclude]
    public int ColumnNumber { get; }
    [JsonInclude]
    public string ErrorMessage { get; }

    public SyntaxErrorException(int line, int column, string message) :
        base("Exception caught: Syntax error at line " +
            line + ", column " + column + ": " + message)
    {
        LineNumber = line;
        ColumnNumber = column;
        ErrorMessage = message;
    }

    protected SyntaxErrorException(SerializationInfo info, StreamingContext context) : base(info, context)
    {

    }

    [Obsolete("This constructor is for serialization purposes only.")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);

        info.AddValue(nameof(LineNumber), LineNumber);
        info.AddValue(nameof(ColumnNumber), ColumnNumber);
        info.AddValue(nameof(ErrorMessage), ErrorMessage);
    }
}