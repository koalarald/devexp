using System;
using System.IO;

namespace SParser
{
    class Program
    {
        static void Main(string[] args)
        {
            var cArgs = new CommandArgs();
            if (!CommandLine.Parser.Default.ParseArguments(args, cArgs))
            {
                Console.WriteLine("Press any key to exit...");
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
                var parser = new ParserContext(cArgs.FilePath, cArgs.FilterColumn, cArgs.FilterValue);
                while (!parser.EndOfData)
                {
                    output = await parser.Load();
                    Console.Write(output);
                }
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine(ex.GetType().FullName);
                Console.WriteLine(ex.Message);
                Console.WriteLine("Press any key to exit...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Press any key to exit...");
            }
        }
    }
}
