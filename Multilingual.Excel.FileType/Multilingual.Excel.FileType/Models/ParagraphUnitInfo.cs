﻿using System.Collections.Generic;
using Sdl.FileTypeSupport.Framework.BilingualApi;

namespace Multilingual.Excel.FileType.Models
{
	public class ParagraphUnitInfo
	{
		public int ExcelSheetIndex { get; set; }

		public string ExcelSheetName { get; set; }

		public uint ExcelRowIndex { get; set; }

		public int ExcelCharacterLimitation { get; set; }

		public int ExcelPixelLimitation { get; set; }

		public string ExcelPixelFontName { get; set; }

		public float ExcelPixelFontSize { get; set; }

		public string ExcelFilterBackgroundColor { get; set; }

		public bool ExcelFilterLockSegments { get; set; }

		public bool IsCDATA { get; set; }

		public string ParagraphUnitId { get; set; }

		public string FileId { get; set; }

		public IParagraphUnit ParagraphUnit { get; set; }

		public List<SegmentPairInfo> SegmentPairs { get; set; }
	}
}
