// FieldRepresentation.cs - Internal model for a C++ class field
namespace Transpiler.Core.Models
{
    public enum Visibility
    {
        Public,
        Private,
        Protected
    }

    public class FieldRepresentation
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public Visibility Visibility { get; set; }
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }

        public FieldRepresentation(string name, string type, Visibility visibility)
        {
            Name = name;
            Type = type;
            Visibility = visibility;
            HasGetter = false;
            HasSetter = false;
        }
    }
} 