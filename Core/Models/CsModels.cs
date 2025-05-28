// CsModels.cs - C# model classes for code generation
using System.Collections.Generic;

namespace Transpiler.Core.Models
{
    public class CsClassModel
    {
        public string Name { get; set; } = "";
        public List<string> BaseClasses { get; } = new List<string>();
        public List<CsFieldModel> Fields { get; } = new List<CsFieldModel>();
        public List<CsPropertyModel> Properties { get; } = new List<CsPropertyModel>();
        public List<CsMethodModel> Methods { get; } = new List<CsMethodModel>();
        public string SourceFileName { get; set; } = "";
        public bool IsAbstract { get; set; }
    }

    public class CsFieldModel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Visibility { get; set; } = "private";
    }

    public class CsPropertyModel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
        public string Visibility { get; set; } = "public";
    }

    public class CsMethodModel
    {
        public string Name { get; set; } = "";
        public string ReturnType { get; set; } = "";
        public string Visibility { get; set; } = "public";
        public List<CsParameterModel> Parameters { get; } = new List<CsParameterModel>();
        public bool IsConstructor { get; set; }
        public bool IsDestructor { get; set; }
        public bool IsEquals { get; set; }
        public bool IsOverride { get; set; }
        public bool IsAbstract { get; set; }
        public string? Comment { get; set; }
    }

    public class CsParameterModel
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
} 