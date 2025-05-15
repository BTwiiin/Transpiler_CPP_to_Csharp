// MethodRepresentation.cs - Internal model for a C++ class method
using System.Collections.Generic;

namespace Transpiler.Core.Models
{
    public enum MethodType
    {
        Normal,
        Constructor,
        Destructor,
        Operator
    }

    public class ParameterRepresentation
    {
        public string Name { get; set; }
        public string Type { get; set; }

        public ParameterRepresentation(string name, string type)
        {
            Name = name;
            Type = type;
        }
    }

    public class MethodRepresentation
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }
        public Visibility Visibility { get; set; }
        public MethodType MethodType { get; set; }
        public List<ParameterRepresentation> Parameters { get; } = new List<ParameterRepresentation>();
        public bool IsPureVirtual { get; set; }
        public bool IsOverride { get; set; }
        public bool IsConst { get; set; }

        public MethodRepresentation(string name, string returnType, Visibility visibility, MethodType methodType = MethodType.Normal)
        {
            Name = name;
            ReturnType = returnType;
            Visibility = visibility;
            MethodType = methodType;
            IsPureVirtual = false;
            IsOverride = false;
            IsConst = false;
        }

        public void AddParameter(ParameterRepresentation parameter)
        {
            Parameters.Add(parameter);
        }

        public void AddParameter(string name, string type)
        {
            Parameters.Add(new ParameterRepresentation(name, type));
        }

        public bool IsGetter()
        {
            // Check if this is a getter method (starts with "get_" and has no parameters)
            return Name.StartsWith("get_") && Parameters.Count == 0 && ReturnType != "void";
        }

        public bool IsSetter()
        {
            // Check if this is a setter method (starts with "set_" and has exactly one parameter)
            return Name.StartsWith("set_") && Parameters.Count == 1 && ReturnType == "void";
        }

        public string? GetPropertyName()
        {
            if (IsGetter() || IsSetter())
            {
                // Convert get_propertyName/set_propertyName to PropertyName (capitalized)
                string baseName = Name.Substring(4); // Remove 'get_' or 'set_'
                return char.ToUpper(baseName[0]) + baseName.Substring(1);
            }
            return null;
        }
    }
} 