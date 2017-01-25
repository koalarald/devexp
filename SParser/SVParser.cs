using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ude;

namespace SParser
{
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
        List<T> Parse();
        Task<List<T>> ParseAsync();
        bool EndOfData { get; }
        string FilePath { get; }
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
            this.DataChunkSize = 30000;
        }

        #endregion

        #region Properties

        public string FilePath { get; private set; }
        public virtual bool EndOfData { get; private set; }
        protected virtual long Position { get; set; }
        protected int DataChunkSize { get; set; }

        #endregion

        #region Constants

        protected const string QuoteExpected = "Quote expected in line {0}.";
        protected const string WrongFields = "Wrong number of fields({0}) in line number {1}. Expected {2}.";
        protected const string EmptyLine = "Empty line number {0}";
        protected const string QuoteNotAllowed = "Quote is not allowed in a not quoted field value. Line {0}.";
        protected const string EndOfStream = "End of stream reached.";
        protected const char Delimiter = ',';
        protected const char Quote = '\"';
        protected const char CarriageReturn = '\r';
        protected const char LineFeed = '\n';
        protected const char Space = ' ';

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
                throw new EndOfStreamException(EndOfStream);
            }
            var allReadChars = 0;
            var expectQuoteOrComma = false;
            var isFieldQuoted = false;
            var lineFields = new List<string>();
            var results = new List<List<string>>();
            var field = new StringBuilder(string.Empty);
            var buffer = new BufferInfo();

            while ((buffer = this.Read(buffer.Position)).ReadChars > 0)
            {
                for (int i = 0; i < buffer.ReadChars; i++)
                {
                    switch (buffer.ContentBuffer[i])
                    {
                        case Delimiter:
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

                            break;
                        case Quote:
                            if (!isFieldQuoted)
                            {
                                if (!string.IsNullOrEmpty(field.ToString()))
                                {
                                    throw new InvalidDataException(string.Format(QuoteNotAllowed, results.Count + 1));
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

                        case CarriageReturn:
                            if (isFieldQuoted && !expectQuoteOrComma)
                            {
                                field.Append(buffer.ContentBuffer[i]);
                            }
                            break;
                        case LineFeed:
                            if (string.IsNullOrEmpty(field.ToString()))
                            {
                                throw new InvalidDataException(string.Format(EmptyLine, results.Count + 1));
                            }
                            if (!isFieldQuoted || expectQuoteOrComma)
                            {
                                if (results.Count > 0 && results[0].Count != lineFields.Count + 1)
                                {
                                    throw new InvalidDataException(string.Format(WrongFields,
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

                        default:
                            if (Char.IsWhiteSpace(buffer.ContentBuffer[i]))
                            {
                                if (expectQuoteOrComma)
                                {
                                    throw new InvalidDataException(string.Format(QuoteExpected, results.Count + 1));
                                }
                                if (isFieldQuoted)
                                {
                                    field.Append(buffer.ContentBuffer[i]);
                                }
                            }
                            else
                            {
                                if (expectQuoteOrComma)
                                {
                                    throw new InvalidDataException(string.Format(QuoteExpected, results.Count + 1));
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
                if (allReadChars > this.DataChunkSize)
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
                    throw new InvalidDataException(string.Format(QuoteExpected, results.Count + 1));
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
        public SVExtendedParser(string filePath) : base(filePath)
        {
        }

        protected virtual Encoding FileEncoding { get; set; }

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

