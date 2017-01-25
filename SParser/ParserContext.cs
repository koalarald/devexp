using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SParser
{
    /// <summary>
    /// Parser Context Facade
    /// </summary>
    public class ParserContext
    {
        #region Constructors

        public ParserContext(string filePath, string filterColumn = null, string filterValue = null)
        {
            this.FilePath = filePath;
            this.FilterColumn = filterColumn;
            this.FilterValue = filterValue;
        }

        #endregion

        #region Properties

        public virtual bool EndOfData
        {
            get
            {
                if (this.Parser == null)
                {
                    return false;
                }
                return this.Parser.EndOfData;
            }
        }
        protected virtual IParser<List<string>> Parser { get; set; }
        private string FilePath { get; set; }
        private string FilterColumn { get; set; }
        private string FilterValue { get; set; }

        #endregion

        #region Fields

        protected const string DataFormatAlignment = "{0,-20}";
        protected const string DataFormatSeparator = "|{0}|";
        protected const string DataFormatShort = "|{0}...|";

        #endregion

        #region Methods

        /// <summary>
        /// Load parsed data
        /// </summary>
        /// <returns>Output of parsing</returns>
        public virtual async Task<string> Load()
        {
            if (this.Parser == null)
            {
                this.Parser = new SVExtendedParser(this.FilePath);
            }
            string Results = string.Empty;
            if (!this.Parser.EndOfData)
            {
                List<List<string>> dataList = await this.Parser.ParseAsync();

                if (!string.IsNullOrEmpty(this.FilterColumn) && !string.IsNullOrEmpty(this.FilterValue)
                    && dataList.Count > 0 && dataList[0].Contains(this.FilterColumn))
                {
                    dataList = await this.FilterDataAsync(dataList);
                }
                Results = await this.FormatDataAsync(dataList);
            }
            return Results;
        }

        protected virtual List<List<string>> FilterData(List<List<string>> dataList)
        {
            var Results = new List<List<string>>();
            List<string> header = dataList[0];
            int filterColumnIndex = header.IndexOf(this.FilterColumn);
            Results = dataList.Where(p => p[filterColumnIndex].Equals(this.FilterValue, StringComparison.InvariantCultureIgnoreCase)).ToList();
            Results.Insert(0, header);

            return Results;
        }

        protected virtual async Task<List<List<string>>> FilterDataAsync(List<List<string>> dataList)
        {
            return await Task.Run(() => this.FilterData(dataList));
        }

        protected virtual string FormatData(List<List<string>> dataList)
        {
            StringBuilder results = new StringBuilder();
            foreach (List<string> fieldsLine in dataList)
            {
                foreach (string field in fieldsLine)
                {
                    results.Append(string.Format(DataFormatAlignment, field.Length < 20 ? string.Format(DataFormatSeparator, field) : string.Format(DataFormatShort, field.Substring(0, 14))));
                }
                results.AppendLine();
            }
            return results.ToString();
        }

        protected virtual async Task<string> FormatDataAsync(List<List<string>> dataList)
        {
            return await Task.Run(() => this.FormatData(dataList));
        }

        #endregion
    }
}
