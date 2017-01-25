using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ude;

namespace SParser
{
    public enum DelimiterType
    {
        Comma = ',',
        Pipe = '|'
    }
    /// <summary>
    /// Data Buffer
    /// </summary>
    public class BufferInfo
    {
        public BufferInfo()
        {
            this.Position = 0;
            this.ReadChars = 0;
            this.ContentBuffer = new char[8192];
        }
        public BufferInfo(int position, int readChars, char[] contentBuffer) : this()
        {
            this.Position = position;
            this.ReadChars = readChars;
            this.ContentBuffer = contentBuffer;
        }
        public long Position { get; set; }
        public int ReadChars { get; set; }
        public char[] ContentBuffer { get; set; }
    }
    /// <summary>
    /// Interface for parsers
    /// </summary>
    /// <typeparam name="T">Type of the output of parsing process</typeparam>
    public interface IParser<T>
    {
        DelimiterType Delimiter { get; }
        List<T> Parse();
        Task<List<T>> ParseAsync();
        bool EndOfData { get; }
        string FilePath { get; set; }
    }

    /// <summary>
    /// SV Parser
    /// </summary>
    public class SVParser : IParser<List<string>>
    {
        #region Constructors

        public SVParser(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException("File path is null.");
            }
            this.FilePath = filePath;
            this.EndOfData = false;
            this.Delimiter = DelimiterType.Comma;
            this.DataBreakLimit = 30000;
        }

        #endregion

        #region Properties

        public string FilePath { get; set; }
        public virtual DelimiterType Delimiter { get; }
        public virtual bool EndOfData { get; private set; }
        protected virtual long Position { get; set; }
        protected int DataBreakLimit { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Read chunk of data
        /// </summary>
        /// <param name="position">Position in stream</param>
        /// <returns>Buffer information object</returns>
        protected virtual BufferInfo Read(long position)
        {
            BufferInfo buffer = new BufferInfo();
            using (var fs = new FileStream(this.FilePath, FileMode.Open))
            {
                fs.Position = position;
                var sr = new StreamReader(fs, Encoding.Default, true);
                buffer.ReadChars = sr.Read(buffer.ContentBuffer, 0, buffer.ContentBuffer.Length);
                buffer.Position = fs.Position;
            }
            return buffer;
        }
        /// <summary>
        /// Parsing method
        /// </summary>
        /// <returns>List of results</returns>
        public virtual List<List<string>> Parse()
        {
            if (this.EndOfData)
            {
                throw new EndOfStreamException("End of stream reached.");
            }
            var allReadChars = 0;
            var expectQuoteOrComma = false;
            var isFieldQuoted = false;
            var lineFields = new List<string>();
            var results = new List<List<string>>();
            var field = new StringBuilder(string.Empty);
            var buffer = new BufferInfo();
            if (this.Position != 0)
            {
                buffer.Position = this.Position;
            }

            while ((buffer = this.Read(buffer.Position)).ReadChars > 0)
            {
                for (int i = 0; i < buffer.ReadChars; i++)
                {
                    switch (buffer.ContentBuffer[i])
                    {
                        case '\"':
                            if (!isFieldQuoted)
                            {
                                if (!string.IsNullOrEmpty(field.ToString()))
                                {
                                    throw new InvalidDataException(string.Format("Quote is not allowed in a not quoted field value. Line {0}.", results.Count + 1));
                                }
                                isFieldQuoted = true;
                            }
                            else
                            {
                                if (!expectQuoteOrComma)
                                {
                                    expectQuoteOrComma = true;
                                }
                                else
                                {
                                    expectQuoteOrComma = false;
                                    field.Append(buffer.ContentBuffer[i]);
                                }
                            }

                            break;

                        case '\r':
                            if (isFieldQuoted && !expectQuoteOrComma)
                            {
                                field.Append(buffer.ContentBuffer[i]);
                            }
                            break;
                        case '\n':
                            if (string.IsNullOrEmpty(field.ToString()))
                            {
                                throw new InvalidDataException(string.Format("Empty line number {0}", results.Count + 1));
                            }
                            if (!isFieldQuoted || expectQuoteOrComma)
                            {
                                if (results.Count > 0 && results[0].Count != lineFields.Count + 1)
                                {
                                    throw new InvalidDataException(string.Format("Wrong number of fields({0}) in line number {1}. Expected {2}.",
                                        lineFields.Count, results.Count + 1, results[0].Count + 1));
                                }

                                isFieldQuoted = false;
                                expectQuoteOrComma = false;

                                lineFields.Add(field.ToString());
                                results.Add(lineFields);
                                field.Clear();
                                lineFields = new List<string>();
                            }
                            else
                            {
                                field.Append(buffer.ContentBuffer[i]);
                            }

                            break;

                        case ' ':
                            if (expectQuoteOrComma)
                            {
                                throw new InvalidDataException(string.Format("Quote expected in line {0}.", results.Count + 1));
                            }
                            if (isFieldQuoted)
                            {
                                field.Append(buffer.ContentBuffer[i]);
                            }
                            break;

                        default:
                            if (buffer.ContentBuffer[i] == (char)this.Delimiter)
                            {
                                if (!isFieldQuoted || expectQuoteOrComma)
                                {
                                    expectQuoteOrComma = false;
                                    isFieldQuoted = false;

                                    lineFields.Add(field.ToString());
                                    field.Clear();
                                }
                                else
                                {
                                    field.Append(buffer.ContentBuffer[i]);
                                }
                            }
                            else
                            {
                                if (expectQuoteOrComma)
                                {
                                    throw new InvalidDataException(string.Format("Quote expected in line {0}.", results.Count + 1));
                                }
                                field.Append(buffer.ContentBuffer[i]);
                            }
                            break;
                    }
                }
                allReadChars += buffer.ReadChars;
                if (buffer.ReadChars < buffer.ContentBuffer.Length)
                {
                    this.EndOfData = true;
                }
                if (allReadChars > this.DataBreakLimit)
                {
                    this.Position = buffer.Position;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(field.ToString()))
            {
                if ((!isFieldQuoted || expectQuoteOrComma))
                {
                    lineFields.Add(field.ToString());
                    results.Add(lineFields);
                    field.Clear();
                }
                else
                {
                    throw new InvalidDataException(string.Format("Quote expected in line {0}.", results.Count + 1));
                }
            }
            return results;
        }
        /// <summary>
        /// Async Parsing method
        /// </summary>
        /// <returns>List of results</returns>
        public virtual async Task<List<List<string>>> ParseAsync()
        {
            return await Task.Run(() => this.Parse());
        }
        #endregion

    }
    /// <summary>
    /// SV Extended Parser
    /// </summary>
    public class SVExtendedParser : SVParser
    {
        protected virtual Encoding FileEncoding { get; set; }
        public SVExtendedParser(string filePath) : base(filePath)
        {
        }

        #region Methods

        protected virtual Encoding GetFileEncoding()
        {
            var encDetector = new CharsetDetector();

            using (var fs = new FileStream(this.FilePath, FileMode.Open))
            {
                byte[] buffer = new byte[8192];
                int readBytes;
                while ((readBytes = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    encDetector.Feed(buffer, 0, readBytes);
                }
                encDetector.DataEnd();
            }

            return Encoding.GetEncoding(encDetector.Charset);
        }
        protected override BufferInfo Read(long position)
        {
            if (this.FileEncoding == null)
            {
                this.FileEncoding = this.GetFileEncoding();
            }
            BufferInfo buffer = new BufferInfo();
            using (var fs = new FileStream(this.FilePath, FileMode.Open))
            {
                fs.Position = position;
                var sr = new StreamReader(fs, this.FileEncoding, false);
                buffer.ReadChars = sr.Read(buffer.ContentBuffer, 0, buffer.ContentBuffer.Length);
                buffer.Position = fs.Position;
            }
            return buffer;
        }

        #endregion
    }
}

