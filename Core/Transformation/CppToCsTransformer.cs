// CppToCsTransformer.cs - Transforms C++ models to C# equivalents
using System.Collections.Generic;
using System.Linq;
using System;
using Transpiler.Core.Models;

namespace Transpiler.Core.Transformation
{
    public class CppToCsTransformer
    {
        // Type mapping from C++ to C#
        private readonly Dictionary<string, string> _typeMapping = new Dictionary<string, string>
        {
            { "int", "int" },
            { "double", "double" },
            { "float", "float" },
            { "char", "char" },
            { "bool", "bool" },
            { "void", "void" },
            { "std::string", "string" },
            { "string", "string" }
        };

        public List<CsClassModel> TransformClasses(List<ClassRepresentation> cppClasses)
        {
            List<CsClassModel> csClasses = new List<CsClassModel>();
            
            foreach (var cppClass in cppClasses)
            {
                CsClassModel csClass = TransformClass(cppClass);
                csClasses.Add(csClass);
            }
            
            return csClasses;
        }

        private CsClassModel TransformClass(ClassRepresentation cppClass)
        {
            CsClassModel csClass = new CsClassModel
            {
                Name = cppClass.Name,
                SourceFileName = cppClass.SourceFileName,
                IsAbstract = cppClass.IsAbstract
            };
            
            // Add base classes
            foreach (var baseClass in cppClass.BaseClasses)
            {
                csClass.BaseClasses.Add(baseClass);
            }
            
            // Transform fields and collect getter/setter methods
            Dictionary<string, MethodRepresentation> getters = new Dictionary<string, MethodRepresentation>();
            Dictionary<string, MethodRepresentation> setters = new Dictionary<string, MethodRepresentation>();
            
            // First identify all getters and setters
            foreach (var method in cppClass.Methods)
            {
                if (method.IsGetter())
                {
                    string propertyName = method.GetPropertyName();
                    Console.WriteLine($"Found getter: {method.Name} -> Property: {propertyName}");
                    getters[propertyName] = method;
                }
                else if (method.IsSetter())
                {
                    string propertyName = method.GetPropertyName();
                    Console.WriteLine($"Found setter: {method.Name} -> Property: {propertyName}");
                    setters[propertyName] = method;
                }
            }
            
            // Transform fields, checking if they should be properties
            foreach (var field in cppClass.Fields)
            {
                string fieldName = field.Name;
                string propertyName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);
                
                bool hasGetter = getters.ContainsKey(propertyName);
                bool hasSetter = setters.ContainsKey(propertyName);
                
                Console.WriteLine($"Processing field: {fieldName} -> Property: {propertyName}, HasGetter: {hasGetter}, HasSetter: {hasSetter}");
                
                if (hasGetter || hasSetter)
                {
                    // This field should be a property
                    Console.WriteLine($"Converting field '{fieldName}' to property '{propertyName}'");
                    
                    // Determine the property visibility from the getter/setter methods
                    Visibility propertyVisibility = field.Visibility; // Default to field visibility
                    if (hasGetter && getters.ContainsKey(propertyName))
                    {
                        propertyVisibility = getters[propertyName].Visibility;
                    }
                    else if (hasSetter && setters.ContainsKey(propertyName))
                    {
                        propertyVisibility = setters[propertyName].Visibility;
                    }
                    
                    CsPropertyModel property = new CsPropertyModel
                    {
                        Name = propertyName,
                        Type = MapType(field.Type),
                        HasGetter = hasGetter,
                        HasSetter = hasSetter,
                        Visibility = ConvertVisibility(propertyVisibility)
                    };
                    
                    csClass.Properties.Add(property);
                }
                else
                {
                    // Regular field
                    Console.WriteLine($"Keeping field '{fieldName}' as field");
                    CsFieldModel csField = new CsFieldModel
                    {
                        Name = field.Name,
                        Type = MapType(field.Type),
                        Visibility = ConvertVisibility(field.Visibility)
                    };
                    
                    csClass.Fields.Add(csField);
                }
            }
            
            // Transform methods (excluding getters/setters that became properties)
            foreach (var method in cppClass.Methods)
            {
                // Skip methods that were transformed to properties
                if (method.IsGetter() || method.IsSetter())
                {
                    Console.WriteLine($"Skipping method '{method.Name}' because it was converted to a property");
                    continue;
                }
                
                Console.WriteLine($"Transforming method: {method.Name}");
                CsMethodModel csMethod = TransformMethod(method);
                csClass.Methods.Add(csMethod);
            }
            
            return csClass;
        }

        private CsMethodModel TransformMethod(MethodRepresentation cppMethod)
        {
            CsMethodModel csMethod = new CsMethodModel
            {
                Name = cppMethod.Name,
                ReturnType = MapType(cppMethod.ReturnType),
                Visibility = ConvertVisibility(cppMethod.Visibility),
                IsAbstract = cppMethod.IsPureVirtual,
                IsOverride = cppMethod.IsOverride
            };
            
            // Transform parameters
            foreach (var param in cppMethod.Parameters)
            {
                CsParameterModel csParam = new CsParameterModel
                {
                    Name = string.IsNullOrEmpty(param.Name) ? "param" : param.Name, // Default name if missing
                    Type = MapType(param.Type)
                };
                
                csMethod.Parameters.Add(csParam);
            }
            
            // Handle special method types
            switch (cppMethod.MethodType)
            {
                case MethodType.Constructor:
                    csMethod.IsConstructor = true;
                    csMethod.Name = cppMethod.Name; // Constructor name is class name
                    break;
                    
                case MethodType.Destructor:
                    csMethod.IsDestructor = true;
                    // Use the class name (remove ~ and substring to extract just class name)
                    string className = cppMethod.Name.Substring(1); // Remove the ~ from ~ClassName
                    csMethod.Name = className;
                    csMethod.ReturnType = ""; // Destructors don't have return types in C#
                    csMethod.Comment = "// TODO: C# destructors work differently from C++";
                    break;
                    
                case MethodType.Operator:
                    if (cppMethod.Name == "operator==")
                    {
                        csMethod.IsEquals = true;
                        csMethod.Name = "Equals";
                        csMethod.ReturnType = "bool";
                        csMethod.IsOverride = true;
                        
                        // Replace parameters with the standard object parameter
                        csMethod.Parameters.Clear();
                        csMethod.Parameters.Add(new CsParameterModel { Name = "other", Type = "object" });
                    }
                    else
                    {
                        csMethod.Comment = $"// TODO: Implement operator {cppMethod.Name.Substring(8)}";
                    }
                    break;
            }

            // Handle const methods
            if (cppMethod.IsConst)
            {
                csMethod.Comment = "// TODO: C# doesn't have const methods, consider using readonly properties or immutable types";
            }
            
            return csMethod;
        }

        private string MapType(string cppType)
        {
            if (string.IsNullOrEmpty(cppType))
            {
                return "";
            }

            // Handle collection types
            if (cppType.StartsWith("std::vector<") || cppType.StartsWith("std::list<"))
            {
                return MapCollectionType(cppType, "List");
            }
            else if (cppType.StartsWith("std::set<"))
            {
                return MapCollectionType(cppType, "HashSet");
            }
            else if (cppType.StartsWith("std::map<"))
            {
                return MapDictionaryType(cppType);
            }
            
            // Check the mapping dictionary
            if (_typeMapping.TryGetValue(cppType, out string csType))
            {
                return csType;
            }
            
            // If it's a pointer or reference, handle specially
            if (cppType.EndsWith("*") || cppType.EndsWith("&"))
            {
                string baseType = cppType.Substring(0, cppType.Length - 1).Trim();
                
                // Try to map the base type
                if (_typeMapping.TryGetValue(baseType, out string baseCs))
                {
                    return baseCs; // In C#, reference types are passed by reference by default
                }
                
                return baseType; // Assume it's a custom type
            }
            
            // Default: assume it's a custom type
            return cppType;
        }

        private string MapCollectionType(string cppType, string collectionType)
        {
            // Extract the element type from the collection
            int startIndex = cppType.IndexOf('<') + 1;
            int endIndex = cppType.LastIndexOf('>');
            string elementType = cppType.Substring(startIndex, endIndex - startIndex).Trim();
            
            // Map the element type
            string mappedElementType = MapType(elementType);
            
            return $"{collectionType}<{mappedElementType}>";
        }

        private string MapDictionaryType(string cppType)
        {
            // Extract the key and value types from the map
            int startIndex = cppType.IndexOf('<') + 1;
            int endIndex = cppType.LastIndexOf('>');
            string types = cppType.Substring(startIndex, endIndex - startIndex).Trim();
            
            // Split the types by comma
            string[] typeParts = types.Split(',');
            if (typeParts.Length != 2)
            {
                throw new Exception($"Invalid map type format: {cppType}");
            }
            
            // Map both types
            string keyType = MapType(typeParts[0].Trim());
            string valueType = MapType(typeParts[1].Trim());
            
            return $"Dictionary<{keyType}, {valueType}>";
        }

        private string ConvertVisibility(Visibility cppVisibility)
        {
            switch (cppVisibility)
            {
                case Visibility.Public:
                    return "public";
                case Visibility.Private:
                    return "private";
                case Visibility.Protected:
                    return "protected";
                default:
                    return "private";
            }
        }
    }
} 