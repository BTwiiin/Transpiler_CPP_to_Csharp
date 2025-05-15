// Parser.cs - Parses tokens into class representations
using System;
using System.Collections.Generic;
using System.IO;
using Transpiler.Core.Lexing;
using Transpiler.Core.Models;
using System.Linq;

namespace Transpiler.Core.Parsing
{
    public class Parser
    {
        private readonly Scanner _scanner;
        private Token _currentToken;
        private string _sourceFileName;
        private List<ClassRepresentation> _classes = new List<ClassRepresentation>();
        private Visibility _currentVisibility = Visibility.Private; // Default visibility in C++
        private readonly HashSet<string> _keywords = new HashSet<string>
        {
            "class", "public", "private", "protected", 
            "const", "static", "void", "int", "double", "bool", "string",
            "operator", "override", "std::vector", "std::list", "std::map", "std::set"
        };

        public Parser(Scanner scanner, string sourceFileName)
        {
            _scanner = scanner;
            _sourceFileName = sourceFileName;
            _currentToken = GetNextMeaningfulToken();
        }

        private Token GetNextMeaningfulToken()
        {
            Token token;
            do
            {
                token = _scanner.GetNextToken();
            }
            while (token.Type == TokenType.Whitespace || token.Type == TokenType.Comment);
            
            return token;
        }

        private void Consume()
        {
            _currentToken = GetNextMeaningfulToken();
        }

        private bool Match(TokenType type, string? lexeme = null)
        {
            return _currentToken.Type == type && 
                  (lexeme == null || _currentToken.Lexeme == lexeme);
        }

        private void Expect(TokenType type, string? lexeme = null)
        {
            if (!Match(type, lexeme))
            {
                string expected = lexeme == null ? type.ToString() : $"{type} '{lexeme}'";
                string found = $"{_currentToken.Type} '{_currentToken.Lexeme}'";
                
                throw new Exception($"Parse error at line {_currentToken.Line}, column {_currentToken.Column}: Expected {expected}, found {found}");
            }
            
            Consume();
        }

        public List<ClassRepresentation> Parse()
        {
            while (_currentToken.Type != TokenType.EndOfFile)
            {
                if (Match(TokenType.Keyword, "class"))
                {
                    ParseClass();
                }
                else
                {
                    // Skip non-class declarations
                    Consume();
                }
            }
            
            return _classes;
        }

        private void ParseClass()
        {
            // Consume 'class' keyword
            Expect(TokenType.Keyword, "class");
            
            // Parse class name
            if (!Match(TokenType.Identifier))
            {
                throw new Exception($"Expected class name at line {_currentToken.Line}, column {_currentToken.Column}");
            }
            
            string className = _currentToken.Lexeme;
            Consume();
            
            // Create class representation
            ClassRepresentation classRep = new ClassRepresentation(className, Path.GetFileName(_sourceFileName));

            // Check for redefinition
            if (_classes.Exists(c => c.Name == className))
            {
                throw new Exception($"Class '{className}' redefined at line {_currentToken.Line}, column {_currentToken.Column}");
            }
            
            // Check for inheritance
            if (Match(TokenType.Symbol, ":"))
            {
                ParseInheritance(classRep);
            }
            
            // Expect opening brace '{'
            Expect(TokenType.Symbol, "{");
            
            // Reset visibility to default
            _currentVisibility = Visibility.Private;
            
            // Parse class body
            ParseClassBody(classRep);
            
            // Expect closing brace '}'
            Expect(TokenType.Symbol, "}");
            
            // Optional semicolon after class definition
            if (Match(TokenType.Symbol, ";"))
            {
                Consume();
            }
            
            _classes.Add(classRep);
        }

        private void ParseInheritance(ClassRepresentation classRep)
        {
            // Consume ':' symbol
            Expect(TokenType.Symbol, ":");
            
            // Parse base classes
            do
            {
                string accessSpecifier = "public"; // Default access specifier
                
                // Check for access specifier
                if (Match(TokenType.Keyword, "public") || 
                    Match(TokenType.Keyword, "protected") || 
                    Match(TokenType.Keyword, "private"))
                {
                    accessSpecifier = _currentToken.Lexeme;
                    Consume();
                }
                
                // Expect base class name
                if (!Match(TokenType.Identifier))
                {
                    throw new Exception($"Expected base class name at line {_currentToken.Line}, column {_currentToken.Column}");
                }
                
                string baseClassName = _currentToken.Lexeme;
                Consume();
                
                classRep.AddBaseClass(baseClassName, accessSpecifier);
                
                // Check for comma (another base class)
                if (Match(TokenType.Symbol, ","))
                {
                    Consume();
                }
                else
                {
                    break;
                }
            }
            while (true);
        }

        private void ParseClassBody(ClassRepresentation classRep)
        {
            Console.WriteLine("\nStarting to parse class body...");
            while (!Match(TokenType.Symbol, "}"))
            {
                Console.WriteLine($"Current token: {_currentToken.Type} '{_currentToken.Lexeme}' at line {_currentToken.Line}");
                
                // Handle visibility sections
                if (Match(TokenType.Keyword, "public") || 
                    Match(TokenType.Keyword, "private") || 
                    Match(TokenType.Keyword, "protected"))
                {
                    string visibility = _currentToken.Lexeme;
                    Console.WriteLine($"Found visibility section: {visibility}");
                    Consume();
                    
                    // Expect colon
                    if (!Match(TokenType.Symbol, ":"))
                    {
                        throw new Exception($"Expected ':' after visibility specifier at line {_currentToken.Line}, column {_currentToken.Column}");
                    }
                    Consume();
                    
                    switch (visibility)
                    {
                        case "public":
                            _currentVisibility = Visibility.Public;
                            break;
                        case "private":
                            _currentVisibility = Visibility.Private;
                            break;
                        case "protected":
                            _currentVisibility = Visibility.Protected;
                            break;
                    }
                    continue; // Skip to next iteration after handling visibility
                }
                // Check for destructor
                else if (Match(TokenType.Keyword, "virtual") || Match(TokenType.Operator, "~"))
                {
                    bool isVirtual = false;
                    if (Match(TokenType.Keyword, "virtual"))
                    {
                        isVirtual = true;
                        Consume();
                    }
                    ParseDestructor(classRep, isVirtual);
                }
                // Check for operator overload
                else if (Match(TokenType.Keyword, "operator"))
                {
                    ParseOperator(classRep);
                }
                // Regular field or method
                else if (Match(TokenType.Identifier) || Match(TokenType.Keyword))
                {
                    Console.WriteLine("Found identifier or keyword, might be a method or field");
                    
                    // Parse the type
                    string typeName = ParseType();
                    Console.WriteLine($"Parsed type: {typeName}");
                    
                    // Special handling for virtual destructor
                    if (typeName == "virtual" && Match(TokenType.Operator, "~"))
                    {
                        ParseDestructor(classRep, true);
                        continue;
                    }
                    
                    // Check for operator keyword
                    if (Match(TokenType.Keyword, "operator"))
                    {
                        Consume(); // Consume 'operator'
                        
                        // Get operator type
                        if (!Match(TokenType.Operator))
                        {
                            throw new Exception($"Expected operator at line {_currentToken.Line}, column {_currentToken.Column}");
                        }
                        
                        string operatorSymbol = _currentToken.Lexeme;
                        Consume();
                        
                        // Create operator method representation
                        string name = "operator" + operatorSymbol;
                        MethodRepresentation operatorMethod = new MethodRepresentation(name, typeName, _currentVisibility, MethodType.Operator);
                        
                        // Parse parameters
                        ParseParameters(operatorMethod);
                        
                        // Check for const qualifier
                        if (Match(TokenType.Keyword, "const"))
                        {
                            operatorMethod.IsConst = true;
                            Consume();
                        }
                        
                        // Expect semicolon
                        Expect(TokenType.Symbol, ";");
                        
                        classRep.AddMethod(operatorMethod);
                        continue;
                    }
                    
                    // Check if it's the class name (constructor)
                    if (Match(TokenType.Identifier, classRep.Name))
                    {
                        string name = _currentToken.Lexeme;
                        Consume();
                        
                        // Check if it's a constructor (followed by '(') or a return type
                        if (Match(TokenType.Symbol, "("))
                        {
                            // It's a constructor
                            MethodRepresentation constructor = new MethodRepresentation(name, "", _currentVisibility, MethodType.Constructor);
                            ParseParameters(constructor);
                            
                            // Handle constructor body or semicolon
                            if (Match(TokenType.Symbol, "{"))
                            {
                                // Skip constructor body
                                int braceCount = 1;
                                Consume(); // Skip opening brace
                                
                                while (braceCount > 0 && !Match(TokenType.EndOfFile))
                                {
                                    if (Match(TokenType.Symbol, "{"))
                                    {
                                        braceCount++;
                                    }
                                    else if (Match(TokenType.Symbol, "}"))
                                    {
                                        braceCount--;
                                    }
                                    
                                    Consume();
                                }
                            }
                            else
                            {
                                // Expect semicolon for constructor declarations
                                Expect(TokenType.Symbol, ";");
                            }
                            
                            classRep.AddMethod(constructor);
                        }
                        else
                        {
                            // It's a return type, continue with method parsing
                            ParseMethod(classRep, name, _currentToken.Lexeme);
                        }
                    }
                    // Could also be a constructor declaration without a return type
                    else if (typeName == classRep.Name && !Match(TokenType.Symbol, ";"))
                    {
                        // This is a constructor declared without return type
                        // Only handle it if the next token is not a semicolon, to avoid
                        // processing forward declarations twice
                        string name = typeName;
                        MethodRepresentation constructor = new MethodRepresentation(name, "", _currentVisibility, MethodType.Constructor);
                        
                        // Parse parameters
                        ParseParameters(constructor);
                        
                        // Handle constructor body or semicolon
                        if (Match(TokenType.Symbol, "{"))
                        {
                            // Skip constructor body
                            int braceCount = 1;
                            Consume(); // Skip opening brace
                            
                            while (braceCount > 0 && !Match(TokenType.EndOfFile))
                            {
                                if (Match(TokenType.Symbol, "{"))
                                {
                                    braceCount++;
                                }
                                else if (Match(TokenType.Symbol, "}"))
                                {
                                    braceCount--;
                                }
                                
                                Consume();
                            }
                        }
                        else
                        {
                            // Expect semicolon for constructor declarations
                            Expect(TokenType.Symbol, ";");
                        }
                        
                        classRep.AddMethod(constructor);
                    }
                    else if (Match(TokenType.Identifier))
                    {
                        // It's a field or method
                        string name = _currentToken.Lexeme;
                        Console.WriteLine($"Found identifier: {name}");
                        Consume();
                        
                        if (Match(TokenType.Symbol, "("))
                        {
                            Console.WriteLine($"Found opening parenthesis, this is a method: {name}");
                            // It's a method
                            ParseMethod(classRep, typeName, name);
                        }
                        else
                        {
                            Console.WriteLine($"No opening parenthesis, this is a field: {name}");
                            // It's a field
                            ParseField(classRep, typeName, name);
                        }
                    }
                    else if (Match(TokenType.Symbol, "*") || Match(TokenType.Symbol, "&"))
                    {
                        // Pointer or reference type - append to type name
                        typeName += _currentToken.Lexeme;
                        Consume();
                        
                        if (Match(TokenType.Identifier))
                        {
                            string name = _currentToken.Lexeme;
                            Consume();
                            
                            if (Match(TokenType.Symbol, "("))
                            {
                                // It's a method
                                ParseMethod(classRep, typeName, name);
                            }
                            else
                            {
                                // It's a field
                                ParseField(classRep, typeName, name);
                            }
                        }
                        else
                        {
                            throw new Exception($"Expected identifier after pointer/reference at line {_currentToken.Line}, column {_currentToken.Column}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unexpected token after type: {_currentToken.Type} '{_currentToken.Lexeme}'");
                        // Skip unexpected tokens
                        Consume();
                    }
                }
                else
                {
                    Console.WriteLine($"Unexpected token: {_currentToken.Type} '{_currentToken.Lexeme}'");
                    // Skip unexpected tokens
                    Consume();
                }
            }
            Console.WriteLine("Finished parsing class body");
        }

        private void ParseField(ClassRepresentation classRep, string typeName, string name)
        {
            // Field already has name and type, now just look for semicolon
            Expect(TokenType.Symbol, ";");
            
            // Create field representation
            FieldRepresentation field = new FieldRepresentation(name, typeName, _currentVisibility);
            classRep.AddField(field);
        }

        private void ParseMethod(ClassRepresentation classRep, string returnType, string name)
        {
            Console.WriteLine($"\nParsing method: {name} (return type: {returnType})");
            
            // Create method representation
            MethodRepresentation method = new MethodRepresentation(name, returnType, _currentVisibility);
            
            // Parse parameters
            ParseParameters(method);
            Console.WriteLine($"Parsed parameters: {method.Parameters.Count}");
            
            // Check if method with the same name and parameters already exists
            Console.WriteLine("Checking for existing methods...");
            foreach (var existingMethod in classRep.Methods)
            {
                Console.WriteLine($"Comparing with: {existingMethod.Name} (Const: {existingMethod.IsConst}, Parameters: {existingMethod.Parameters.Count})");
            }
            
            if (classRep.Methods.Any(m => 
                m.Name == name && 
                m.Parameters.Count == method.Parameters.Count &&
                ParametersMatch(m.Parameters, method.Parameters)))
            {
                Console.WriteLine($"ERROR: Method '{name}' redefined at line {_currentToken.Line}, column {_currentToken.Column}");
                throw new Exception($"Method '{name}' redefined at line {_currentToken.Line}, column {_currentToken.Column}");
            }
            else if (classRep.Fields.Exists(f => f.Name == name))
            {
                Console.WriteLine($"ERROR: Field '{name}' redefined at line {_currentToken.Line}, column {_currentToken.Column}");
                throw new Exception($"Field '{name}' redefined at line {_currentToken.Line}, column {_currentToken.Column}");
            }

            // Check for const method
            if (Match(TokenType.Keyword, "const"))
            {
                method.IsConst = true;
                Console.WriteLine("Found const qualifier");
                Consume(); // Consume 'const'
            }
            
            // Check for override keyword
            if (Match(TokenType.Keyword, "override"))
            {
                method.IsOverride = true;
                Consume(); // Consume 'override'
            }
            
            // Check for pure virtual method (= 0)
            if (Match(TokenType.Operator, "="))
            {
                Consume(); // Consume =
                
                if (Match(TokenType.NumberLiteral, "0"))
                {
                    method.IsPureVirtual = true;
                    Console.WriteLine("Found pure virtual method");
                    Consume(); // Consume 0
                }
            }
            
            // Handle method body or semicolon
            if (Match(TokenType.Symbol, "{"))
            {
                // Skip method body
                int braceCount = 1;
                Consume(); // Skip opening brace
                
                while (braceCount > 0 && !Match(TokenType.EndOfFile))
                {
                    if (Match(TokenType.Symbol, "{"))
                    {
                        braceCount++;
                    }
                    else if (Match(TokenType.Symbol, "}"))
                    {
                        braceCount--;
                    }
                    
                    Consume();
                }
            }
            else
            {
                // Expect semicolon for method declarations
                Expect(TokenType.Symbol, ";");
            }
            
            Console.WriteLine($"Adding method to class: {name} (Const: {method.IsConst}, PureVirtual: {method.IsPureVirtual})");
            classRep.AddMethod(method);
        }

        private void ParseParameters(MethodRepresentation method)
        {
            // Consume '('
            Expect(TokenType.Symbol, "(");
            
            // Check for empty parameter list
            if (Match(TokenType.Symbol, ")"))
            {
                Consume();
                return;
            }
            
            // Parse parameters
            do
            {
                // Handle 'const' modifier
                if (Match(TokenType.Keyword, "const"))
                {
                    Consume();
                }
                
                // Get parameter type
                if (!Match(TokenType.Identifier) && !Match(TokenType.Keyword))
                {
                    throw new Exception($"Expected parameter type at line {_currentToken.Line}, column {_currentToken.Column}");
                }
                
                string paramType = ParseType();
                
                // Handle reference/pointer types
                if (Match(TokenType.Operator, "&") || Match(TokenType.Operator, "*"))
                {
                    paramType += _currentToken.Lexeme;
                    Consume();
                }
                
                // Get parameter name (might be omitted)
                string paramName = "";
                if (Match(TokenType.Identifier))
                {
                    paramName = _currentToken.Lexeme;
                    Consume();
                }
                
                method.AddParameter(paramName, paramType);
                
                // Check for comma (another parameter)
                if (Match(TokenType.Symbol, ","))
                {
                    Consume();
                }
                else
                {
                    break;
                }
            }
            while (true);
            
            // Expect closing parenthesis
            Expect(TokenType.Symbol, ")");
        }

        private void ParseConstructor(ClassRepresentation classRep)
        {
            string name = _currentToken.Lexeme;
            Consume();
            
            // Create constructor representation
            MethodRepresentation constructor = new MethodRepresentation(name, "", _currentVisibility, MethodType.Constructor);
            
            // Parse parameters
            ParseParameters(constructor);
            
            // Handle constructor body or semicolon
            if (Match(TokenType.Symbol, "{"))
            {
                // Skip constructor body
                int braceCount = 1;
                Consume(); // Skip opening brace
                
                while (braceCount > 0 && !Match(TokenType.EndOfFile))
                {
                    if (Match(TokenType.Symbol, "{"))
                    {
                        braceCount++;
                    }
                    else if (Match(TokenType.Symbol, "}"))
                    {
                        braceCount--;
                    }
                    
                    Consume();
                }
            }
            else
            {
                // Expect semicolon for constructor declarations
                Expect(TokenType.Symbol, ";");
            }
            
            classRep.AddMethod(constructor);
        }

        private void ParseDestructor(ClassRepresentation classRep, bool isVirtual = false)
        {
            // Consume '~'
            Expect(TokenType.Operator, "~");
            
            // Expect class name
            if (!Match(TokenType.Identifier, classRep.Name))
            {
                throw new Exception($"Expected class name after '~' at line {_currentToken.Line}, column {_currentToken.Column}");
            }
            
            string name = "~" + _currentToken.Lexeme;
            Consume();
            
            // Create destructor representation
            MethodRepresentation destructor = new MethodRepresentation(name, "void", _currentVisibility, MethodType.Destructor);
            destructor.IsVirtual = isVirtual;
            
            // Parse parameters (should be empty)
            ParseParameters(destructor);
            
            // Expect semicolon
            Expect(TokenType.Symbol, ";");
            
            classRep.AddMethod(destructor);
        }

        private void ParseOperator(ClassRepresentation classRep)
        {
            // Consume 'operator' keyword
            Consume();
            
            // Get operator type
            if (!Match(TokenType.Operator))
            {
                throw new Exception($"Expected operator at line {_currentToken.Line}, column {_currentToken.Column}");
            }
            
            string operatorSymbol = _currentToken.Lexeme;
            Consume();
            
            // Create operator method representation
            string name = "operator" + operatorSymbol;
            string returnType = "bool"; // Default return type for comparison operators
            
            MethodRepresentation operatorMethod = new MethodRepresentation(name, returnType, _currentVisibility, MethodType.Operator);
            
            // Parse parameters
            ParseParameters(operatorMethod);
            
            // Check for const qualifier
            if (Match(TokenType.Keyword, "const"))
            {
                operatorMethod.IsConst = true;
                Consume();
            }
            
            // Expect semicolon
            Expect(TokenType.Symbol, ";");
            
            classRep.AddMethod(operatorMethod);
        }

        private string ParseType()
        {
            string typeName = _currentToken.Lexeme;
            Consume();

            // Skip virtual keyword
            if (typeName == "virtual")
            {
                // Check if next token is ~ (destructor)
                if (Match(TokenType.Operator, "~"))
                {
                    // This is a virtual destructor, let ParseClassBody handle it
                    return "virtual";
                }
                
                if (!Match(TokenType.Identifier) && !Match(TokenType.Keyword))
                {
                    throw new Exception($"Expected type after 'virtual' at line {_currentToken.Line}, column {_currentToken.Column}");
                }
                typeName = _currentToken.Lexeme;
                Consume();
            }

            // Check if it's a collection type
            if (typeName == "std::vector" || typeName == "std::list" || typeName == "std::set")
            {
                return ParseSingleTypeCollection(typeName);
            }
            else if (typeName == "std::map")
            {
                return ParseMapCollection();
            }

            // Handle pointer/reference types
            if (Match(TokenType.Operator, "*") || Match(TokenType.Operator, "&"))
            {
                typeName += _currentToken.Lexeme;
                Consume();
            }

            return typeName;
        }

        private string ParseSingleTypeCollection(string collectionType)
        {
            // Expect opening angle bracket
            Expect(TokenType.Symbol, "<");
            
            // Parse the element type
            string elementType = ParseType();
            
            // Expect closing angle bracket
            Expect(TokenType.Symbol, ">");
            
            return $"{collectionType}<{elementType}>";
        }

        private string ParseMapCollection()
        {
            // Expect opening angle bracket
            Expect(TokenType.Symbol, "<");
            
            // Parse the key type
            string keyType = ParseType();
            
            // Expect comma
            Expect(TokenType.Symbol, ",");
            
            // Parse the value type
            string valueType = ParseType();
            
            // Expect closing angle bracket
            Expect(TokenType.Symbol, ">");
            
            return $"std::map<{keyType},{valueType}>";
        }
    
        private bool ParametersMatch(List<ParameterRepresentation> existingParams, List<ParameterRepresentation> newParams)
        {
            if (existingParams.Count != newParams.Count)
            {
                return false;
            }

            for (int i = 0; i < existingParams.Count; i++)
            {
                if (existingParams[i].Type != newParams[i].Type)
                {
                    return false;
                }
            }

            return true;
        }
    }
} 