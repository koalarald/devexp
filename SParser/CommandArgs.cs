using CommandLine;
using CommandLine.Text;

namespace SParser
{
    internal class CommandArgs
    {
        [Option('f', "filepath", Required = true, HelpText = "File Path")]
        public virtual string FilePath { get; set; }

        [Option('c', "filtercolumn", HelpText = "Column to be filtered")]
        public virtual string FilterColumn { get; set; }

        [Option('v', "filtervalue", HelpText = "Value to be filtered by")]
        public virtual string FilterValue { get; set; }

        [HelpOption]
        public virtual string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
