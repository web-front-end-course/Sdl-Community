﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using NLog;
using TMX_Lib.Db;

namespace TMX_Lib.XmlSplit
{
	/*
	 *
	 * Assumptions:
	 * - the xml syntax is simple: we have the _root element, that contains the _body, and the _body contains an array of _elements
	 * - the header, if present, is always fully present in the first block
	 */
	public class XmlSplitter : IDisposable
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private string _fileName;

		// the idea for the pad extra - just in case we read, at the end a partial UTF8 char, decoding it can trigger an exception
		// in that case, we simply need a few extra bytes, so that the last UTF8 char is fully read
		private int PAD_EXTRA = 128;

		public int SplitSize = 64 * 1024 * 1024;

		private XmlDocument _document;

		private FileStream _stream;

		private string _rootXmlName ;
		private string _bodyXmlName;
		private string _elementXmlName;

		private string _firstLine = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n";

		private bool _firstBlock = true;
		private string _remainingFromLastBlock = "";

		private bool _eofReached = false;
		private byte[] _buffer;
		private int _offset;

		private Encoding _encoding;
		public bool EndOfStreamReached  => _eofReached;

		private long _blockCount;
		private long _currentBlockIndex;

		public XmlSplitter(string fileName, string rootXmlName = "tmx", string bodyXmlName = "body", string elementXmlName = "tu")
		{
			_fileName = fileName;
			_rootXmlName = rootXmlName;
			_bodyXmlName = bodyXmlName;
			_elementXmlName = elementXmlName;
			if (!File.Exists(fileName))
				throw new TmxException($"file not found: {fileName}");

			try
			{
				_blockCount = new FileInfo(fileName).Length / SplitSize;
				_encoding = GetEncoding(fileName);
				_stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				log.Debug($"splitting {fileName} - into {_blockCount}, found encoding {_encoding.ToString()}");
			}
			catch (Exception e)
			{
				throw new TmxException($"can't open file for reading {fileName}");
			}
		}

		// simple way to see where we are (0 to 1)
		public double Progress()
		{
			lock (this)
			{
				if (_eofReached)
					return 1;
				var percent = (double)_currentBlockIndex / (double)_blockCount;
				return Math.Min(percent, 1);
			}
		}

		// https://stackoverflow.com/questions/3825390/effective-way-to-find-any-files-encoding
		private static Encoding GetEncoding(string filename)
		{
			// Read the BOM
			var bom = new byte[4];
			using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read))
			{
				file.Read(bom, 0, 4);
			}

			// Analyze the BOM
			if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
			if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
			if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0) return Encoding.UTF32; //UTF-32LE
			if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
			if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
			if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return new UTF32Encoding(true, true);  //UTF-32BE

			// We actually have no idea what the encoding is if we reach this point, so
			// you may wish to return null instead of defaulting to ASCII
			return Encoding.ASCII;
		}

		// tries to get the same sub-document
		// if it returns null, there are no more sub-documents
		public string TryGetNextString()
		{
			log.Debug($"parsing {_fileName} - block {_currentBlockIndex} / {_blockCount}");
			lock (this)
			{
				if (_eofReached)
					return null;

				++_currentBlockIndex;
			}

			StringBuilder builder = new StringBuilder();
			if (_firstBlock)
				_buffer = new byte[SplitSize + PAD_EXTRA];

			if (_firstBlock && ReferenceEquals(_encoding, Encoding.ASCII))
			{
				// in this case, read the header
				var headerSize = _stream.Read(_buffer, 0, 1024);
				var headerString = _encoding.GetString(_buffer, 0, headerSize);
				if (headerString.StartsWith("<?"))
				{
					var encodingStart = headerString.IndexOf("encoding=\"", StringComparison.InvariantCultureIgnoreCase);
					if (encodingStart >= 0)
					{
						encodingStart += "encoding=\"".Length;
						var encodingEnd = headerString.IndexOf("\"", encodingStart);
						if (encodingEnd >= 0)
						{
							var encodingStr = headerString.Substring(encodingStart, encodingEnd - encodingStart).ToLower();
							switch (encodingStr)
							{
								case "utf-8": _encoding = Encoding.UTF8; break;
								case "utf-7": _encoding = Encoding.UTF7; break;
								case "utf-16": _encoding = Encoding.Unicode; break;
								case "utf-32": _encoding = Encoding.UTF32; break;
								default:
									throw new TmxException($"Invalid .tmx Encoding: {encodingStr}");
									break;
							}
						}
					}
				}

				_stream.Seek(0, SeekOrigin.Begin);
				log.Debug($"Encoding for {_fileName} overridden : {_encoding.ToString()}");
			}

			var readByteCount = _stream.Read(_buffer, 0, SplitSize);
			if (readByteCount < 1)
			{
				lock(this)
					_eofReached = true;
				return null;
			}

			_offset += readByteCount;
			string curString = "";
			while(true)
				try
				{
					// this can throw if the last char hasn't been fully read
					curString = _encoding.GetString(_buffer, 0, readByteCount);
					break;
				}
				catch
				{
					var readByte = _stream.Read(_buffer, readByteCount, 1);
					++readByteCount;
					++_offset;
					if (readByte != 1)
						throw new TmxException($"The file {_fileName} contains invalid characters");
				}

			var xmlHeader = _firstLine;
			if (curString.StartsWith("<?"))
				xmlHeader = ""; // already have the header

			if (!_firstBlock)
				xmlHeader += $"<{_rootXmlName}> <{_bodyXmlName}> ";

			curString = xmlHeader + _remainingFromLastBlock + curString;
			var hasEnd = curString.Contains($"</{_rootXmlName}>");
			var hasEndBody = curString.Contains($"</{_bodyXmlName}>");

			if (!hasEnd && !hasEndBody)
			{
				var endElementXml = $"</{_elementXmlName}>";
				var lastElementIdx = curString.LastIndexOf(endElementXml);
				if (lastElementIdx < 0)
					throw new TmxException($"Invalid file {_fileName} - did not find closing {endElementXml}");

				lastElementIdx += endElementXml.Length;
				_remainingFromLastBlock = curString.Substring(lastElementIdx);
				curString = curString.Substring(0, lastElementIdx) + $"</{_bodyXmlName}></{_rootXmlName}>";
			}
			else
			{
				// has end of end-of-body
				_remainingFromLastBlock = "";

				if (!hasEndBody)
					curString += $"</{_bodyXmlName}>";
				if (!hasEnd)
					curString += $"</{_rootXmlName}>";
			}

			_firstBlock = false;
			return curString;
		}

		// tries to get the same sub-document
		// if it returns null, there are no more sub-documents
		public XmlDocument TryGetNextSubDocument()
		{
			var str = TryGetNextString();
			if (str == null)
				return null;

			try
			{
				XmlReaderSettings settings = new XmlReaderSettings();
				settings.XmlResolver = null;
				settings.DtdProcessing = DtdProcessing.Ignore;

				var bytes = Encoding.UTF8.GetBytes(str);
				using (var memoryStream = new MemoryStream(bytes))
				using (var reader = new StreamReader(memoryStream))
				using (var xmlReader = XmlTextReader.Create(reader, settings))
				{
					var document = new XmlDocument();
					document.Load(xmlReader);
					return document;
				}
			}
			catch (Exception e)
			{
				throw new TmxException($"Invalid .tmx file [{Path.GetFileNameWithoutExtension(_fileName)}], while parsing sub-block {_currentBlockIndex} of {_currentBlockIndex}", e);
			}
		}

		public void Dispose()
		{
			_document = null;
			_stream?.Dispose();
			_stream = null;
		}
	}
}
