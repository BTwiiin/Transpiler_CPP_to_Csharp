// TokenLogger.cs - Utility to log all tokens to a file for analysis
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Transpiler.Core.Models;

namespace Transpiler.Core.Lexing
{
    public class TokenLogger
    {
        private readonly Scanner _scanner;
        private readonly string _outputFilePath;
        private readonly List<Token> _tokens;

        public TokenLogger(Scanner scanner, string outputFilePath = "tokens_output.txt")
        {
            _scanner = scanner;
            _outputFilePath = outputFilePath;
            _tokens = new List<Token>();
        }

        /// <summary>
        /// Scans all tokens and writes them to the specified file
        /// </summary>
        public void ScanAndWriteAllTokens()
        {
            Console.WriteLine($"Starting token analysis, output will be written to: {_outputFilePath}");
            
            _tokens.Clear();
            Token token;
            int tokenCount = 0;

            // Collect all tokens
            do
            {
                token = _scanner.GetNextToken();
                _tokens.Add(token);
                tokenCount++;
                
                // Progress indicator for large files
                if (tokenCount % 100 == 0)
                {
                    Console.Write($"{token.Lexeme} ");
                }
            } 
            while (token.Type != TokenType.EndOfFile);

            Console.WriteLine($"\nScanned {tokenCount} tokens. Writing to file...");

            // Write tokens to file
            WriteTokensToFile();
            
            Console.WriteLine($"Token analysis complete! Check {_outputFilePath} for results.");
        }

        /// <summary>
        /// Writes all collected tokens to the output file in a readable format
        /// </summary>
        private void WriteTokensToFile()
        {
            using (var writer = new StreamWriter(_outputFilePath, false, Encoding.UTF8))
            {
                // Write header
                writer.WriteLine("TOKEN ANALYSIS REPORT");
                writer.WriteLine("===================");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"Total Tokens: {_tokens.Count}");
                writer.WriteLine();

                // Write summary statistics
                WriteSummaryStatistics(writer);
                writer.WriteLine();

                // Write detailed token list
                WriteDetailedTokenList(writer);
                
                // Write tokens grouped by type
                writer.WriteLine();
                WriteTokensByType(writer);
            }
        }

        /// <summary>
        /// Writes summary statistics about token types
        /// </summary>
        private void WriteSummaryStatistics(StreamWriter writer)
        {
            writer.WriteLine("SUMMARY STATISTICS");
            writer.WriteLine("------------------");

            var tokenTypeCounts = new Dictionary<TokenType, int>();
            
            foreach (var token in _tokens)
            {
                if (tokenTypeCounts.ContainsKey(token.Type))
                    tokenTypeCounts[token.Type]++;
                else
                    tokenTypeCounts[token.Type] = 1;
            }

            foreach (var kvp in tokenTypeCounts)
            {
                writer.WriteLine($"{kvp.Key,-20}: {kvp.Value,6} ({(kvp.Value * 100.0 / _tokens.Count):F1}%)");
            }
        }

        /// <summary>
        /// Writes detailed list of all tokens with position information
        /// </summary>
        private void WriteDetailedTokenList(StreamWriter writer)
        {
            writer.WriteLine("DETAILED TOKEN LIST");
            writer.WriteLine("-------------------");
            writer.WriteLine($"{"#",-6} {"Type",-20} {"Value",-30} {"Line",-6} {"Column",-8}");
            writer.WriteLine(new string('-', 72));

            for (int i = 0; i < _tokens.Count; i++)
            {
                var token = _tokens[i];
                string displayValue = EscapeSpecialCharacters(token.Lexeme);
                
                // Truncate long values for display
                if (displayValue.Length > 28)
                {
                    displayValue = displayValue.Substring(0, 25) + "...";
                }

                writer.WriteLine($"{i + 1,-6} {token.Type,-20} {displayValue,-30} {token.Line,-6} {token.Column,-8}");
            }
        }

        /// <summary>
        /// Writes tokens grouped by their type for easier analysis
        /// </summary>
        private void WriteTokensByType(StreamWriter writer)
        {
            writer.WriteLine("TOKENS GROUPED BY TYPE");
            writer.WriteLine("----------------------");

            var tokensByType = new Dictionary<TokenType, List<Token>>();
            
            foreach (var token in _tokens)
            {
                if (!tokensByType.ContainsKey(token.Type))
                    tokensByType[token.Type] = new List<Token>();
                
                tokensByType[token.Type].Add(token);
            }

            foreach (var kvp in tokensByType)
            {
                writer.WriteLine($"\n{kvp.Key} ({kvp.Value.Count} tokens):");
                writer.WriteLine(new string('-', kvp.Key.ToString().Length + 20));
                
                foreach (var token in kvp.Value)
                {
                    string displayValue = EscapeSpecialCharacters(token.Lexeme);
                    writer.WriteLine($"  Line {token.Line,3}, Col {token.Column,3}: \"{displayValue}\"");
                }
            }
        }

        /// <summary>
        /// Escapes special characters for better readability in the output file
        /// </summary>
        private string EscapeSpecialCharacters(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "<empty>";

            return value
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\"", "\\\"")
                .Replace("\\", "\\\\");
        }

        /// <summary>
        /// Gets the list of collected tokens (useful for programmatic analysis)
        /// </summary>
        public List<Token> GetTokens()
        {
            return new List<Token>(_tokens);
        }

        /// <summary>
        /// Writes a simplified token list (just type and value) for quick analysis
        /// </summary>
        public void WriteSimpleTokenList(string outputPath = "simple_tokens.txt")
        {
            using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
            {
                writer.WriteLine("SIMPLE TOKEN LIST");
                writer.WriteLine("=================");
                writer.WriteLine();

                foreach (var token in _tokens)
                {
                    writer.WriteLine($"{token.Type}: \"{EscapeSpecialCharacters(token.Lexeme)}\"");
                }
            }
            
            Console.WriteLine($"Simple token list written to: {outputPath}");
        }
    }

    /// <summary>
    /// Usage example class showing how to use the TokenLogger
    /// </summary>
    public static class TokenLoggerExample
    {
        public static void AnalyzeSourceFile(string sourceFilePath, string outputDir = ".")
        {
            try
            {
                // Create input reader for the source file
                var inputReader = new InputReader(File.ReadAllText(sourceFilePath));
                
                // Create scanner
                var scanner = new Scanner(inputReader);
                
                // Create output file path
                string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
                string outputPath = Path.Combine(outputDir, $"{fileName}_tokens.txt");
                
                // Create token logger and analyze
                var tokenLogger = new TokenLogger(scanner, outputPath);
                tokenLogger.ScanAndWriteAllTokens();
                
                // Also create a simple version
                string simpleOutputPath = Path.Combine(outputDir, $"{fileName}_simple_tokens.txt");
                tokenLogger.WriteSimpleTokenList(simpleOutputPath);
                
                Console.WriteLine($"Analysis complete for: {sourceFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing file {sourceFilePath}: {ex.Message}");
            }
        }
    }
}