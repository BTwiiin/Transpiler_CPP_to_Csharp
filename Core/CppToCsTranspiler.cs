// CppToCsTranspiler.cs - Main transpiler class that orchestrates the transpilation process

using Transpiler.Core.CodeGeneration;
using Transpiler.Core.Lexing;
using Transpiler.Core.Models;
using Transpiler.Core.Parsing;
using Transpiler.Core.Transformation;

namespace Transpiler.Core
{
    public class CppToCsTranspiler
    {
        private readonly CppToCsTransformer _transformer;
        private readonly CsCodeGenerator _generator;
        private readonly string _outputDirectory;

        public CppToCsTranspiler(string outputDirectory = null)
        {
            _transformer = new CppToCsTransformer();
            _generator = new CsCodeGenerator();
            _outputDirectory = outputDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "Output");
        }

        public void ProcessFile(string filePath)
        {
            try
            {
                // Step 1: Create the input reader
                InputReader reader = new InputReader(filePath);
                
                // Step 2: Create the scanner
                Scanner scanner = new Scanner(reader);
                
                // Step 3: Create the parser and parse the file
                Parser parser = new Parser(scanner, filePath);
                List<ClassRepresentation> cppClasses = parser.Parse();
                
                if (cppClasses.Count == 0)
                {
                    Console.WriteLine($"No classes found in {filePath}");
                    return;
                }
                
                Console.WriteLine($"Found {cppClasses.Count} class(es) in {filePath}");
                
                // Step 4: Transform C++ classes to C# models
                List<CsClassModel> csClasses = _transformer.TransformClasses(cppClasses);
                
                // Step 5: Generate C# code files
                foreach (var csClass in csClasses)
                {
                    _generator.GenerateCode(csClass, _outputDirectory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
} 