using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Transpiler.Core.CustomException;

[Serializable]
[JsonSerializable(typeof(ParsingErrorException))]
public class ParsingErrorException : Exception
{
    [JsonInclude]
    public int LineNumber { get; }
    [JsonInclude]
    public int ColumnNumber { get; }
    [JsonInclude]
    public string ErrorMessage { get; }

    public ParsingErrorException(int line, int column, string message) :
        base("\nParsing error at line " +
            line + ", column " + column + ":\n" + message)
    {
        LineNumber = line;
        ColumnNumber = column;
        ErrorMessage = message;
    }

    protected ParsingErrorException(SerializationInfo info, StreamingContext context) : base(info, context)
    {

    }

    public ParsingErrorException(string? message) : base(message)
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