// ClassRepresentation.cs - Internal model for a C++ class
using System.Collections.Generic;
using System.Linq;
using System;

namespace Transpiler.Core.Models
{
    public class ClassRepresentation
    {
        public string Name { get; set; }
        public List<string> BaseClasses { get; } = new List<string>();
        public List<FieldRepresentation> Fields { get; } = new List<FieldRepresentation>();
        public List<MethodRepresentation> Methods { get; } = new List<MethodRepresentation>();
        public string SourceFileName { get; set; }
        public bool IsAbstract => Methods.Any(m => m.IsPureVirtual);

        public ClassRepresentation(string name, string sourceFileName)
        {
            Name = name;
            SourceFileName = sourceFileName;
        }

        public void AddBaseClass(string baseClassName, string accessModifier = "public")
        {
            // In C#, only public inheritance is supported, so I'll add a note for protected/private
            if (accessModifier != "public")
            {
                // I'll handle this in the transformation phase
            }
            
            if (!BaseClasses.Contains(baseClassName))
            {
                BaseClasses.Add(baseClassName);
            }
        }

        public void AddField(FieldRepresentation field)
        {
            Fields.Add(field);
        }

        public void AddMethod(MethodRepresentation method)
        {
            Console.WriteLine($"Adding method: {method.Name} (Const: {method.IsConst}, Parameters: {method.Parameters.Count})");
            
            // Check if method with same name and parameters already exists
            foreach (var existingMethod in Methods)
            {
                Console.WriteLine($"Checking against existing method: {existingMethod.Name} (Const: {existingMethod.IsConst}, Parameters: {existingMethod.Parameters.Count})");
                
                if (existingMethod.Name == method.Name && 
                    existingMethod.Parameters.Count == method.Parameters.Count)
                {
                    Console.WriteLine("Found method with same name and parameter count, checking parameter types...");
                    
                    // Check if parameters match
                    bool allMatch = true;
                    for (int i = 0; i < method.Parameters.Count; i++)
                    {
                        Console.WriteLine($"Comparing parameter {i}: {existingMethod.Parameters[i].Type} vs {method.Parameters[i].Type}");
                        if (existingMethod.Parameters[i].Type != method.Parameters[i].Type)
                        {
                            allMatch = false;
                            break;
                        }
                    }
                    
                    if (allMatch)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"ERROR: Method '{method.Name}' redefined!");
                        Console.ResetColor();
                        throw new Exception($"Method '{method.Name}' redefined - duplicate method signature detected");
                    }
                }
            }
            
            Methods.Add(method);
            Console.WriteLine($"Successfully added method: {method.Name}");
        }
    }
} 