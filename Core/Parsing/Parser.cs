// Parser.cs - Parses tokens into class representations
using Transpiler.Core.Lexing;
using Transpiler.Core.Models;
using Transpiler.Core.CustomException;
using System;
using System.Formats.Asn1;

namespace Transpiler.Core.Parsing
{
    public class Parser
    {
        private readonly Scanner _scanner;
        private Token _currentToken;
        private string _sourceFileName;
        private List<ClassRepresentation> _classes = new List<ClassRepresentation>();
        private Visibility _currentVisibility = Visibility.Private; // Default visibility in C++
        private Token _markedToken;

        public Parser(Scanner scanner, string sourceFileName)
        {
            _scanner = scanner;
            _sourceFileName = sourceFileName;
            _currentToken = _scanner.GetNextToken();
        }

        private void Consume()
        {
            // Console.WriteLine($"Consuming token: {_currentToken.Type} '{_currentToken.Lexeme}' at line {_currentToken.Line}, column {_currentToken.Column}");
            _currentToken = _scanner.GetNextToken();
        }
        

        // Match the current token with the expected type and lexeme
        private bool Match(TokenType type, string? lexeme = null)
        {
            return _currentToken.Type == type &&
                  (lexeme == null || _currentToken.Lexeme == lexeme);
        }

        // Consume the current token if it matches the expected type and lexeme
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

        private void MarkPosition()
        {
            _scanner.Mark();
            // Store the current token state
            _markedToken = _currentToken;
        }

        private void ResetPosition()
        {
            _scanner.Reset();
            // Restore the token state
            _currentToken = _markedToken;
        }

        private void CommitPosition()
        {
            _scanner.Commit();
            // Clear the marked token
            _markedToken = null;
        }

        public List<ClassRepresentation> Parse()
        {


            while (_currentToken.Type != TokenType.EndOfFile)
            {
                ClassRepresentation? classRep;
                if (TryParseClass(out classRep))
                {
                    _classes.Add(classRep);
                }
                else
                {
                    // Skip non-class declarations
                    Consume();
                }
            }
            Console.WriteLine($"Parsed {_classes.Count} classes");
            foreach (var classRep in _classes)
            {
                Console.WriteLine($"Class: {classRep.Name}");
                foreach (var field in classRep.Fields)
                {
                    Console.WriteLine($"  Field: {field.Name} ({field.Type})");
                }
                foreach (var method in classRep.Methods)
                {
                    Console.WriteLine($"  Method: {method.Name} ({method.ReturnType})");
                }
            }
            
            return _classes;
        }

        // class_declaration ::= "class" identifier inheritance? "{" class_body "}" ";"
        private bool TryParseClass(out ClassRepresentation? classRep)
        {
            if (Match(TokenType.KeywordClass, "class"))
            {
                string className;
                Consume();

                // Check if the class name is already defined
                if (_classes.Any(c => c.Name == _currentToken.Lexeme))
                {
                    throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, $"Class '{_currentToken.Lexeme}' is already defined.");
                }

                if (TryParseIdentifier(out className))
                {
                    classRep = new ClassRepresentation(className, _sourceFileName);

                    /* 
                    inheritance ::= ":" base_class ( "," base_class )* 

                    base_class ::= "public" identifier |  
                                      "protected" identifier  |
                                      "private" identifier  |
                                       identifier  // default public if omitted? 
                    */

                    if (Match(TokenType.Symbol, ":"))
                    {
                        Consume();
                        string baseClassName;

                        // Check for visibility specifier of base class and consume it
                        if (Match(TokenType.KeywordPublic, "public"))
                        {
                            _currentVisibility = Visibility.Public;
                            Consume();
                        }
                        else if (Match(TokenType.KeywordProtected, "protected"))
                        {
                            _currentVisibility = Visibility.Protected;
                            Consume();
                        }
                        else if (Match(TokenType.KeywordPrivate, "private"))
                        {
                            _currentVisibility = Visibility.Private;
                            Consume();
                        }

                        // Check for base class name
                        // C++ supports multiple inheritance, so we can add multiple base classes
                        if (TryParseIdentifier(out baseClassName))
                        {
                            classRep.AddBaseClass(baseClassName);
                            while (Match(TokenType.Symbol, ","))
                            {
                                Consume();
                                if (TryParseIdentifier(out baseClassName))
                                {
                                    classRep.AddBaseClass(baseClassName);
                                }
                                else
                                {
                                    throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, "Expected class name after ','");
                                }
                            }
                        }
                        else
                        {
                            throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, "Expected class name after ':'");
                        }
                    }

                    // Expect '{' to open the class body
                    Expect(TokenType.Symbol, "{");

                    if (TryParseBody(classRep))
                    {
                        // Successfully parsed class body
                    }
                    else
                    {
                        throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, $"Expected class body: error after '{_currentToken.Lexeme}'");
                    }

                    // Expect '}' to close the class body and ';' to end the class declaration
                    Expect(TokenType.Symbol, "}");
                    Expect(TokenType.Symbol, ";");

                    return true;
                }
                else
                {
                    throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, "Expected class name after 'class'");
                }
            }
            else
            {
                classRep = null;
                return false;
            }
        }


        // identifier ::= letter (letter | digit | "_")*
        private bool TryParseIdentifier(out string identifier)
        {
            if (Match(TokenType.Identifier))
            {
                identifier = _currentToken.Lexeme;
                Consume();
                return true;
            }
            identifier = null;
            return false;
        }

        
        // class_body ::= (visibility_section | field_declaration | method_declaration)* 
        private bool TryParseBody(ClassRepresentation classRep)
        {
            Console.WriteLine($"Starting to parse body of class '{classRep.Name}'");
            // Loop until we reach the closing '}' of the class body
            while (!Match(TokenType.Symbol, "}"))
            {
                if (_currentToken.Type == TokenType.EndOfFile)
                {
                    throw new SyntaxErrorException(_currentToken.Line, _currentToken.Column, "Unexpected end of file in class body");
                }
                
                // Try to parse each body element type in sequence using scanner-like pattern
                if (TryParseVisibilitySection() ||
                    TryParseOperatorDeclaration(classRep) ||
                    TryParseConstructor(classRep) ||
                    TryParseDestructor(classRep) ||
                    TryParseFieldDeclaration(classRep) ||
                    TryParseMethodDeclaration(classRep))
                {
                    // Successfully parsed one of the body elements
                    // Each TryParse method handles its own success logging
                    continue;
                }
                else
                {
                    // If none matched, consume the token and continue
                    Console.WriteLine($"Failed to parse element in class body at token: {_currentToken.Type} '{_currentToken.Lexeme}' at line {_currentToken.Line}, column {_currentToken.Column}");
                    Consume();
                }
            }
            // Return true if we stopped because of '}', false otherwise
            bool result = Match(TokenType.Symbol, "}");
            Console.WriteLine($"Finished parsing body of class '{classRep.Name}'. Success: {result}");
            return result;
        }


        //constructor_declaration ::= identifier "(" parameter_list? ")" ";" 
        private bool TryParseConstructor(ClassRepresentation classRep)
        {
            Console.WriteLine($"Trying to parse constructor for class '{classRep.Name}'. Current token: {_currentToken.Lexeme}");
            if (Match(TokenType.Identifier, classRep.Name))
            {
                string constructorName = _currentToken.Lexeme;
                Consume();
                Console.WriteLine($"Check for \"(\"");
                Expect(TokenType.Symbol, "(");

                List<ParameterRepresentation> parameters = new List<ParameterRepresentation>();
                if (TryParseParameterList(parameters))
                {
                    // Successfully parsed parameter list
                }

                Expect(TokenType.Symbol, ")");
                Expect(TokenType.Symbol, ";");
                
                var constructor = new MethodRepresentation(constructorName, classRep.Name, _currentVisibility, MethodType.Constructor);
                foreach (var param in parameters)
                {
                    constructor.AddParameter(param);
                }
                classRep.AddMethod(constructor);
                Console.WriteLine($"Parsed constructor for class '{classRep.Name}'");
                return true;
            }
            return false;
        }

        // destructor_declaration ::= "~" identifier "(" ")" ";"
        private bool TryParseDestructor(ClassRepresentation classRep)
        {
            Console.WriteLine($"Trying to parse destructor for class '{classRep.Name}'. Current token: {_currentToken.Lexeme}");
            if (Match(TokenType.Symbol, "~"))
            {
                Consume();
                if (Match(TokenType.Identifier, classRep.Name))
                {
                    string destructorName = "~" + _currentToken.Lexeme;
                    Consume();
                    Expect(TokenType.Symbol, "(");
                    Expect(TokenType.Symbol, ")");
                    Expect(TokenType.Symbol, ";");
                    
                    classRep.AddMethod(new MethodRepresentation(destructorName, "void", _currentVisibility, MethodType.Destructor));
                    Console.WriteLine($"Parsed destructor for class '{classRep.Name}'");
                    return true;
                }
                else
                {
                    throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, $"Expected class name '{classRep.Name}' after '~'");
                }
            }
            return false;
        }

        // parameter_list ::= parameter ( "," parameter )*
        private bool TryParseParameterList(List<ParameterRepresentation> parameters)
        {
            Console.WriteLine($"Trying to parse parameter list. Current token: {_currentToken.Lexeme}");
            
            // If we immediately see a closing parenthesis, there are no parameters
            if (Match(TokenType.Symbol, ")"))
            {
                return true;
            }

            // Parse first parameter
            ParameterRepresentation parameter;
            if (!TryParseParameter(out parameter))
            {
                return false;
            }
            parameters.Add(parameter);

            // Parse additional parameters separated by commas
            while (Match(TokenType.Symbol, ","))
            {
                Consume(); // consume the comma
                if (TryParseParameter(out parameter))
                {
                    parameters.Add(parameter);
                }
                else
                {
                    throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, "Expected parameter after ','");
                }
            }

            return true;
        }

        // parameter ::= "const"? type "&"? identifier?
        private bool TryParseParameter(out ParameterRepresentation parameter)
        {
            Console.WriteLine($"Trying to parse parameter. Current token: {_currentToken.Lexeme}");
            parameter = null;
            
            bool isConst = false;
            bool isReference = false;
            string paramType;
            string paramName = "";

            // Check for const qualifier
            if (Match(TokenType.KeywordConst, "const"))
            {
                isConst = true;
                Consume();
            }

            // Parse the type
            if (!TryParseType(out paramType))
            {
                return false;
            }

            // Add const prefix if it was present
            if (isConst)
            {
                paramType = "const " + paramType;
            }

            // Check for reference indicator
            if (Match(TokenType.Symbol, "&"))
            {
                isReference = true;
                paramType += "&";
                Consume();
            }

            // Parse parameter name (optional in some contexts like function declarations)
            if (Match(TokenType.Identifier))
            {
                paramName = _currentToken.Lexeme;
                Consume();
            }
            else
            {
                // If no name is provided, generate a default parameter name
                paramName = $"param{DateTime.Now.Ticks % 1000}";
            }

            parameter = new ParameterRepresentation(paramName, paramType);
            Console.WriteLine($"Parsed parameter: {paramType} {paramName}");
            return true;
        }

        // virtual_spec ::= "virtual"
        private bool TryParseVirtualSpec(out bool isVirtual)
        {
            isVirtual = false;
            if (Match(TokenType.KeywordVirtual, "virtual"))
            {
                isVirtual = true;
                Consume();
                return true;
            }
            return false;
        }

        // const_spec ::= "const"
        private bool TryParseConstSpec(out bool isConst)
        {
            isConst = false;
            if (Match(TokenType.KeywordConst, "const"))
            {
                isConst = true;
                Consume();
                return true;
            }
            return false;
        }

        // override_spec ::= "override"
        private bool TryParseOverrideSpec(out bool isOverride)
        {
            isOverride = false;
            if (Match(TokenType.KeywordOverride, "override"))
            {
                isOverride = true;
                Consume();
                return true;
            }
            return false;
        }

        // pure_virtual ::= "= 0"
        private bool TryParsePureVirtual(out bool isPureVirtual)
        {
            isPureVirtual = false;
            if (Match(TokenType.Symbol, "="))
            {
                Consume();
                if (Match(TokenType.NumberLiteral, "0"))
                {
                    isPureVirtual = true;
                    Consume();
                    return true;
                }
                else
                {
                    throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, "Expected '0' after '=' in pure virtual declaration");
                }
            }
            return false;
        }

        // operator_declaration ::= type "operator" operator_symbol "(" parameter_list? ")" const_spec? ";"
        private bool TryParseOperatorDeclaration(ClassRepresentation classRep)
        {
            MarkPosition();
            Console.WriteLine($"Trying to parse operator declaration. Current token: {_currentToken.Lexeme}");
            
            string returnType;
            
            // Parse the return type first
            if (!TryParseType(out returnType))
            {
                ResetPosition();
                return false;
            }
            
            // Check for operator keyword (we'll need to look for the pattern as identifier)
            if (Match(TokenType.Identifier, "operator"))
            {
                Consume();
                
                // Parse the operator symbol (==, +, -, etc.)
                if (!Match(TokenType.Symbol) && !Match(TokenType.Operator))
                {
                    throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, "Expected operator symbol after 'operator'");
                }

                string operatorSymbol = _currentToken.Lexeme;
                string operatorName = "operator" + operatorSymbol;
                Consume();

                Expect(TokenType.Symbol, "(");

                List<ParameterRepresentation> parameters = new List<ParameterRepresentation>();
                if (TryParseParameterList(parameters))
                {
                    // Successfully parsed parameter list
                }

                Expect(TokenType.Symbol, ")");

                // Check for const specifier
                bool isConst;
                TryParseConstSpec(out isConst);

                Expect(TokenType.Symbol, ";");

                var operatorMethod = new MethodRepresentation(operatorName, returnType, _currentVisibility, MethodType.Operator);
                operatorMethod.IsConst = isConst;
                foreach (var param in parameters)
                {
                    operatorMethod.AddParameter(param);
                }
                classRep.AddMethod(operatorMethod);
                CommitPosition();
                Console.WriteLine($"Parsed operator declaration for class '{classRep.Name}'");
                return true;
            }
            else
            {
                ResetPosition();
                return false;
            }
        }

        // visibility_section ::= ("public" | "protected" | "private") ":" TODO: Add ":" to documentation
        private bool TryParseVisibilitySection()
        {
            if (Match(TokenType.KeywordPublic, "public"))
            {
                _currentVisibility = Visibility.Public;
                Console.WriteLine($"Consuming {_currentToken.Lexeme}");
                Consume();
                Console.WriteLine($"Consuming using Expect {_currentToken.Lexeme}");
                Expect(TokenType.Symbol, ":");
                Console.WriteLine($"Token After Expect {_currentToken.Lexeme}");
                Console.WriteLine($"Parsed visibility section: {_currentVisibility}");
                return true;
            }
            else if (Match(TokenType.KeywordProtected, "protected"))
            {
                _currentVisibility = Visibility.Protected;
                Consume();
                Expect(TokenType.Symbol, ":");
                Console.WriteLine($"Parsed visibility section: {_currentVisibility}");
                return true;
            }
            else if (Match(TokenType.KeywordPrivate, "private"))
            {
                _currentVisibility = Visibility.Private;
                Consume();
                Expect(TokenType.Symbol, ":");
                Console.WriteLine($"Parsed visibility section: {_currentVisibility}");
                return true;
            }
            return false;
        }


        // field_declaration ::= type identifier ";"  TODO: Consider adding support for multiple fields in one declaration
        private bool TryParseFieldDeclaration(ClassRepresentation classRep)
        {
            MarkPosition();
            Console.WriteLine($"Current token marked position: '{_currentToken.Lexeme}'");
            string fieldType;
            string fieldName;

            Console.WriteLine($"Trying to Mark field declaration for token '{_currentToken.Lexeme}'");

            if (_currentToken.Lexeme == classRep.Name)
            {
                // Consume the class name token
                Consume();
                // Check for constructor
                if (Match(TokenType.Symbol, "("))
                {
                    Console.WriteLine($"Constructor detected for class '{classRep.Name}' in Field Declaration");
                    ResetPosition();
                    return false;
                }
            }

            if (!TryParseType(out fieldType))
            {
                ResetPosition();
                return false;
            }

            if (!TryParseIdentifier(out fieldName))
            {
                ResetPosition();
                return false;
            }

            // Check for duplicate field names
            if (classRep.Fields.Any(f => f?.Name == fieldName))
            {
                throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, $"Field '{fieldName}' is already defined in class '{classRep.Name}'.");
            }

            if (Match(TokenType.Symbol, "("))
            {
                ResetPosition();
                Console.WriteLine($"Resetting position for field declaration '{_currentToken.Lexeme}'");
                return false;
            }

            // Consume the ';' token
            Expect(TokenType.Symbol, ";");

            // Add the field to the class representation
            classRep.AddField(new FieldRepresentation(fieldName, fieldType, _currentVisibility));
            CommitPosition();
            Console.WriteLine($"Parsed field: {fieldType} {fieldName} with {_currentVisibility} visibility");
            return true;
        }

        private bool TryParseMethodDeclaration(ClassRepresentation classRep)
        {
            MarkPosition();
            Console.WriteLine($"Trying to parse method declaration for class '{classRep.Name}'. Current token: {_currentToken.Lexeme}");
            
            bool isVirtual = false;
            bool isConst = false;
            bool isOverride = false;
            bool isPureVirtual = false;
            string returnType;
            string methodName;
            List<ParameterRepresentation> parameters = new List<ParameterRepresentation>();

            // Parse optional virtual specifier
            TryParseVirtualSpec(out isVirtual);

            // Parse return type (which may include const)
            if (!TryParseType(out returnType))
            {
                ResetPosition();
                return false;
            }

            // Parse method name
            if (!TryParseIdentifier(out methodName))
            {
                ResetPosition();
                return false;
            }

            // Check for opening parenthesis
            if (!Match(TokenType.Symbol, "("))
            {
                ResetPosition();
                return false;
            }
            Consume();

            // Parse parameters
            if (!TryParseParameterList(parameters))
            {
                ResetPosition();
                return false;
            }
            Consume(); // Consume the closing parenthesis

            // Parse optional const specifier
            TryParseConstSpec(out isConst);

            // Parse optional inheritance specifiers (override or pure virtual)
            TryParseOverrideSpec(out isOverride);
            if (!isOverride)
            {
                TryParsePureVirtual(out isPureVirtual);
            }

            // Expect semicolon
            Expect(TokenType.Symbol, ";");

            // Determine method type based on method name
            MethodType methodType = MethodType.Normal;
            if (methodName.StartsWith("operator"))
            {
                methodType = MethodType.Operator;
            }

            // Create and configure the method representation
            var method = new MethodRepresentation(methodName, returnType, _currentVisibility, methodType);
            method.IsVirtual = isVirtual;
            method.IsConst = isConst;
            method.IsOverride = isOverride;
            method.IsPureVirtual = isPureVirtual;
            
            foreach (var param in parameters)
            {
                method.AddParameter(param);
            }

            // Add the method to the class representation
            classRep.AddMethod(method);
            CommitPosition();
            Console.WriteLine($"Parsed method: {returnType} {methodName} with {_currentVisibility} visibility");
            return true;
        }


        // type ::= "const"? ("int" | "double" | "std::string" | "bool" | identifier | collection_type) ("*"|"&")?
        private bool TryParseType(out string type)
        {
            type = "";
            bool isConst = false;
            
            // Check for const qualifier at the beginning
            if (Match(TokenType.KeywordConst, "const"))
            {
                isConst = true;
                type = "const ";
                Consume();
            }
            
            string baseType;
            if (Match(TokenType.KeywordInt, "int") ||
                Match(TokenType.KeywordDouble, "double") ||
                Match(TokenType.KeywordBool, "bool") ||
                Match(TokenType.KeywordVoid, "void"))
            {
                baseType = _currentToken.Lexeme;
                Consume();
            }
            else if (Match(TokenType.KeywordStd, "std"))
            {
                Consume();
                Expect(TokenType.Symbol, ":");
                Expect(TokenType.Symbol, ":");
                if (Match(TokenType.KeywordString, "string"))
                {
                    baseType = "std::string";
                    Consume();
                }
                else if (Match(TokenType.KeywordStdVector, "vector") ||
                    Match(TokenType.KeywordStdList, "list") ||
                    Match(TokenType.KeywordStdSet, "set"))
                {
                    string collectionType = _currentToken.Lexeme;
                    Consume();
                    Expect(TokenType.Symbol, "<");
                    
                    if (TryParseType(out string innerType))
                    {
                        baseType = $"std::{collectionType}<{innerType}>";
                    }
                    else
                    {
                        throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, $"Expected type after '<' in collection type");
                    }
                    
                    Expect(TokenType.Symbol, ">");
                }
                else if (Match(TokenType.KeywordStdMap, "map"))
                {
                    Consume();
                    Expect(TokenType.Symbol, "<");
                    
                    if (TryParseType(out string keyType))
                    {
                        Expect(TokenType.Symbol, ",");
                        if (TryParseType(out string valueType))
                        {
                            baseType = $"std::map<{keyType}, {valueType}>";
                        }
                        else
                        {
                            throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, $"Expected value type after ',' in map type");
                        }
                    }
                    else
                    {
                        throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, $"Expected key type after '<' in map type");
                    }
                    
                    Expect(TokenType.Symbol, ">");
                }
                else
                {
                    throw new ParsingErrorException(_currentToken.Line, _currentToken.Column, $"Expected collection type after 'std::'");
                }
            }
            else if (Match(TokenType.Identifier))
            {
                baseType = _currentToken.Lexeme;
                Consume();
            }
            else
            {
                return false;
            }
            
            type += baseType;
            
            // Check for pointer or reference indicators
            if (Match(TokenType.Symbol, "*"))
            {
                type += "*";
                Consume();
            }
            else if (Match(TokenType.Symbol, "&"))
            {
                type += "&";
                Consume();
            }
            
            return true;
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

/*
translation_unit  ::= (class_declaration | other_declarations)* 
 
class_declaration ::= "class" identifier inheritance? "{" class_body "}" ";"? 
 
inheritance        ::= ":" base_class ( "," base_class )* 
base_class         ::= "public" identifier   
                    | "protected" identifier  
                    | "private" identifier  
                    | identifier  // default public if omitted?
 
class_body         ::= (visibility_section | field_declaration | method_declaration)* 
 
visibility_section ::= "public:" | "private:" | "protected:" 
 
field_declaration  ::= type identifier ";" 
 
method_declaration ::= virtual_spec? type identifier "(" parameter_list? ")" const_spec? inheritance_spec? ";" 
                    | constructor_declaration 
                    | destructor_declaration 
                    | operator_declaration 
 
Inheritance_spec ::= override_spec | pure_virtual 
virtual_spec ::= "virtual" 
const_spec ::= "const" 
override_spec ::= "override" 
pure_virtual ::= "= 0" 
 
constructor_declaration ::= identifier "(" parameter_list? ")" ";" 
 
destructor_declaration  ::= "~" identifier "(" ")" ";" 
 
parameter_list      ::= parameter ( "," parameter )* 
parameter           ::= type identifier 
 
type                ::= "int" | "double" | "std::string" | identifier | collection_type 
collection_type  ::= "std::vector<" type ">" |  "std::list<" type ">" |  "std::map<" type "," type " >" | "std::set<" type ">" 
identifier          ::= letter (letter | digit | "_")* 
letter              ::= [A-Za-z] 
digit               ::= [0-9] 
*/
