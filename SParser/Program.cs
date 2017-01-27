using System;
using System.IO;

namespace SParser
{
    class Program
    {
        private const string PressAnyKey = "Press any key to exit...";

        static void Main(string[] args)
        {
            var cArgs = new CommandArgs();
            if (!CommandLine.Parser.Default.ParseArguments(args, cArgs))
            {
                Console.WriteLine(PressAnyKey);
            }
            else
            {
                RunParsing(cArgs);
            }
            Console.ReadKey();
        }
        static async void RunParsing(CommandArgs cArgs)
        {
            string output = string.Empty;
            try
            {
                var parser = new ParserContext(new SVExtendedParser(cArgs.FilePath), cArgs.FilterColumn, cArgs.FilterValue);
                //reading in chunks to avoid issues
                while (!parser.EndOfData)
                {
                    output = await parser.Load();
                    Console.Clear();
                    Console.OutputEncoding = parser.FileEncoding;
                    Console.Write(output);
                }
                Console.WriteLine();
                Console.WriteLine(PressAnyKey);
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine(ex.GetType().FullName);
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                Console.WriteLine(PressAnyKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                Console.WriteLine(PressAnyKey);
            }

        }
    }
}
