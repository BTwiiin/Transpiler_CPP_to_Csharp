// CppToCsTranspiler.cs - Main transpiler class that orchestrates the transpilation process

using Transpiler.Core.CodeGeneration;
using Transpiler.Core.Lexing;
using Transpiler.Core.Models;
using Transpiler.Core.Parsing;
using Transpiler.Core.Transformation;
using Transpiler.Core.CustomException;

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
            Console.WriteLine($"Processing: {filePath}");
            
            try
            {
                // Set a timeout to prevent infinite loops
                System.Threading.CancellationTokenSource cts = new System.Threading.CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(10)); // Cancel after 10 seconds
                
                try
                {
                    // Step 1: Create the input reader
                    InputReader reader = new InputReader(filePath);
                    
                    // Step 2: Create the scanner
                    Scanner scanner = new Scanner(reader);
                    
                    // Step 3: Create the parser and parse the file
                    Parser parser = new Parser(scanner, filePath);
                    
                    try
                    {
                        // Start a task to check for cancellation
                        var parseTask = Task.Run(() => parser.Parse(), cts.Token);
                        
                        // Wait for the parsing to complete or timeout
                        if (!parseTask.Wait(10000, cts.Token)) // 10 second timeout
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("ERROR: Parsing timed out after 10 seconds, possible infinite loop detected.");
                            Console.ResetColor();
                            Console.WriteLine("Transpilation failed due to timeout.");
                            return;
                        }
                        
                        List<ClassRepresentation> cppClasses = parseTask.Result;
                    
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
                        
                        Console.WriteLine("Transpilation completed successfully.");
                    }
                    catch (ParsingErrorException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"PARSING ERROR at line {ex.LineNumber}, column {ex.ColumnNumber}: {ex.ErrorMessage}");
                        Console.ResetColor();
                        Console.WriteLine("Transpilation completed with errors.");
                        return; // Exit after parsing error
                    }
                    catch (SyntaxErrorException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"SYNTAX ERROR at line {ex.LineNumber}, column {ex.ColumnNumber}: {ex.ErrorMessage}");
                        Console.ResetColor();
                        Console.WriteLine("Transpilation completed with errors.");
                    }
                    catch (AggregateException ae)
                    {
                        if (ae.InnerExceptions.Any(e => e is OperationCanceledException))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("ERROR: Parsing operation was canceled, possible infinite loop detected.");
                            Console.ResetColor();
                            Console.WriteLine("Transpilation failed due to cancellation.");
                        }
                        else
                        {
                            throw ae.InnerException;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"CRITICAL ERROR processing file {filePath}: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    Console.ResetColor();
                    Console.WriteLine("Transpilation failed.");
                }
                finally
                {
                    cts.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"UNHANDLED ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
        }
    }
} 