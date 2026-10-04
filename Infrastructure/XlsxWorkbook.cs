using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace ShoeStore.Infrastructure
{
    // Tạo file Excel (.xlsx) KHÔNG cần thư viện ngoài.
    // File .xlsx thực chất là 1 file nén zip chứa các file XML theo chuẩn Office Open XML.
    //
    // Cách dùng:
    //   var book = new XlsxWorkbook();
    //   var sheet = book.AddSheet("Đơn hàng", widths: new[] { 10d, 30d });
    //   sheet.AddHeader("Mã đơn", "Khách hàng");
    //   sheet.AddRow(XlsxCell.Number(12), XlsxCell.Text("Nguyễn Văn A"));
    //   byte[] file = book.ToBytes();
    public class XlsxWorkbook
    {
        private readonly List<XlsxSheet> _sheets = new();

        public XlsxSheet AddSheet(string name, double[]? widths = null)
        {
            var sheet = new XlsxSheet(SafeSheetName(name), widths ?? Array.Empty<double>());
            _sheets.Add(sheet);
            return sheet;
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(zip, "[Content_Types].xml", ContentTypesXml());
                WriteEntry(zip, "_rels/.rels", RootRelsXml);
                WriteEntry(zip, "xl/workbook.xml", WorkbookXml());
                WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
                WriteEntry(zip, "xl/styles.xml", StylesXml);

                for (var i = 0; i < _sheets.Count; i++)
                {
                    WriteEntry(zip, $"xl/worksheets/sheet{i + 1}.xml", _sheets[i].ToXml());
                }
            }

            return stream.ToArray();
        }

        // ===================== Các file XML bên trong =====================

        private string ContentTypesXml()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (var i = 1; i <= _sheets.Count; i++)
            {
                sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            sb.Append("</Types>");
            return sb.ToString();
        }

        private const string RootRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private string WorkbookXml()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            for (var i = 0; i < _sheets.Count; i++)
            {
                sb.Append($"<sheet name=\"{Escape(_sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            }
            sb.Append("</sheets></workbook>");
            return sb.ToString();
        }

        private string WorkbookRelsXml()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 1; i <= _sheets.Count; i++)
            {
                sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            }
            sb.Append($"<Relationship Id=\"rId{_sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        // Kiểu ô (thứ tự trong cellXfs = số XlsxStyle bên dưới):
        // 0 thường | 1 tiêu đề cột | 2 tiền | 3 ngày giờ | 4 số nguyên | 5 chữ đậm | 6 tiền đậm | 7 số đậm | 8 tiêu đề lớn | 9 ngày
        private const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"4\">" +
            "<numFmt numFmtId=\"164\" formatCode=\"#,##0 &quot;₫&quot;\"/>" +
            "<numFmt numFmtId=\"165\" formatCode=\"dd/mm/yyyy hh:mm\"/>" +
            "<numFmt numFmtId=\"166\" formatCode=\"#,##0\"/>" +
            "<numFmt numFmtId=\"167\" formatCode=\"dd/mm/yyyy\"/>" +
            "</numFmts>" +
            "<fonts count=\"4\">" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"16\"/><name val=\"Calibri\"/></font>" +
            "</fonts>" +
            "<fills count=\"4\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF212529\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF1F3F5\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"2\">" +
            "<border><left/><right/><top/><bottom/><diagonal/></border>" +
            "<border><left style=\"thin\"><color rgb=\"FFDEE2E6\"/></left><right style=\"thin\"><color rgb=\"FFDEE2E6\"/></right>" +
            "<top style=\"thin\"><color rgb=\"FFDEE2E6\"/></top><bottom style=\"thin\"><color rgb=\"FFDEE2E6\"/></bottom><diagonal/></border>" +
            "</borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"10\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" applyBorder=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"><alignment horizontal=\"left\" vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
            "<xf numFmtId=\"164\" fontId=\"2\" fillId=\"3\" borderId=\"1\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
            "<xf numFmtId=\"166\" fontId=\"2\" fillId=\"3\" borderId=\"1\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
            "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" applyFont=\"1\"/>" +
            "<xf numFmtId=\"167\" fontId=\"0\" fillId=\"0\" borderId=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"><alignment horizontal=\"left\" vertical=\"top\"/></xf>" +
            "</cellXfs>" +
            "</styleSheet>";

        // ===================== Hàm phụ =====================

        private static void WriteEntry(ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        // Tên sheet: tối đa 31 ký tự, không có các ký tự : \ / ? * [ ]
        private static string SafeSheetName(string name)
        {
            var cleaned = new string(name.Where(c => !":\\/?*[]".Contains(c)).ToArray()).Trim();
            if (cleaned.Length == 0) cleaned = "Sheet";
            return cleaned.Length > 31 ? cleaned[..31] : cleaned;
        }

        // Thoát ký tự đặc biệt của XML (& < > ") và bỏ ký tự điều khiển không hợp lệ
        internal static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (!XmlConvert.IsXmlChar(c) && !char.IsSurrogate(c)) continue;
                sb.Append(c switch
                {
                    '&' => "&amp;",
                    '<' => "&lt;",
                    '>' => "&gt;",
                    '"' => "&quot;",
                    _ => c.ToString()
                });
            }
            return sb.ToString();
        }
    }

    public enum XlsxStyle
    {
        Normal = 0,
        Header = 1,
        Money = 2,
        DateTime = 3,
        Integer = 4,
        Bold = 5,
        BoldMoney = 6,
        BoldInteger = 7,
        Title = 8,
        Date = 9
    }

    // 1 ô trong bảng tính: chữ hoặc số, kèm kiểu hiển thị
    public readonly struct XlsxCell
    {
        public string? TextValue { get; }
        public double? NumberValue { get; }
        public XlsxStyle Style { get; }

        private XlsxCell(string? text, double? number, XlsxStyle style)
        {
            TextValue = text;
            NumberValue = number;
            Style = style;
        }

        public static XlsxCell Text(string? value, XlsxStyle style = XlsxStyle.Normal) => new(value ?? "", null, style);
        public static XlsxCell Number(double value, XlsxStyle style = XlsxStyle.Integer) => new(null, value, style);
        public static XlsxCell Money(decimal value, XlsxStyle style = XlsxStyle.Money) => new(null, (double)value, style);
        public static XlsxCell Empty => new(null, null, XlsxStyle.Normal);

        // Excel lưu ngày giờ là SỐ NGÀY tính từ 30/12/1899 (vd: 1,5 = 31/12/1899 12:00)
        public static XlsxCell DateTime(System.DateTime value) =>
            new(null, (value - new System.DateTime(1899, 12, 30)).TotalDays, XlsxStyle.DateTime);

        public static XlsxCell Date(System.DateTime value) =>
            new(null, (value.Date - new System.DateTime(1899, 12, 30)).TotalDays, XlsxStyle.Date);

        // Cho phép viết "abc" hoặc 12 trực tiếp khi gọi AddRow
        public static implicit operator XlsxCell(string? value) => Text(value);
        public static implicit operator XlsxCell(int value) => Number(value);
    }

    public class XlsxSheet
    {
        private readonly List<XlsxCell[]> _rows = new();
        private readonly double[] _widths;
        private int _headerRow = -1;     // dòng tiêu đề cột (để cố định + bật bộ lọc)
        private int _headerColumns;

        public string Name { get; }

        internal XlsxSheet(string name, double[] widths)
        {
            Name = name;
            _widths = widths;
        }

        public void AddRow(params XlsxCell[] cells) => _rows.Add(cells);

        public void AddEmptyRow() => _rows.Add(Array.Empty<XlsxCell>());

        // Dòng tiêu đề cột: nền tối chữ trắng; dòng này được cố định khi cuộn và có nút lọc của Excel
        public void AddHeader(params string[] titles)
        {
            _headerRow = _rows.Count;
            _headerColumns = titles.Length;
            _rows.Add(titles.Select(t => XlsxCell.Text(t, XlsxStyle.Header)).ToArray());
        }

        internal string ToXml()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            // Cố định các dòng tới hết dòng tiêu đề cột
            if (_headerRow >= 0)
            {
                var topLeft = $"A{_headerRow + 2}";
                sb.Append("<sheetViews><sheetView workbookViewId=\"0\">");
                sb.Append($"<pane ySplit=\"{_headerRow + 1}\" topLeftCell=\"{topLeft}\" activePane=\"bottomLeft\" state=\"frozen\"/>");
                sb.Append("</sheetView></sheetViews>");
            }

            if (_widths.Length > 0)
            {
                sb.Append("<cols>");
                for (var i = 0; i < _widths.Length; i++)
                {
                    sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{_widths[i].ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }

            sb.Append("<sheetData>");
            for (var r = 0; r < _rows.Count; r++)
            {
                var rowNumber = r + 1;
                sb.Append($"<row r=\"{rowNumber}\"");
                if (r == _headerRow) sb.Append(" ht=\"22\" customHeight=\"1\"");
                if (_rows[r].Length > 0 && _rows[r][0].Style == XlsxStyle.Title) sb.Append(" ht=\"24\" customHeight=\"1\"");
                sb.Append('>');

                for (var c = 0; c < _rows[r].Length; c++)
                {
                    var cell = _rows[r][c];
                    var reference = ColumnName(c) + rowNumber;
                    var style = (int)cell.Style;

                    if (cell.NumberValue is double number)
                    {
                        sb.Append($"<c r=\"{reference}\" s=\"{style}\"><v>{number.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    }
                    else if (!string.IsNullOrEmpty(cell.TextValue))
                    {
                        // inlineStr: chữ nằm luôn trong ô (không cần bảng sharedStrings)
                        sb.Append($"<c r=\"{reference}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{XlsxWorkbook.Escape(cell.TextValue)}</t></is></c>");
                    }
                    else
                    {
                        sb.Append($"<c r=\"{reference}\" s=\"{style}\"/>");
                    }
                }

                sb.Append("</row>");
            }
            sb.Append("</sheetData>");

            // Nút lọc (mũi tên) trên dòng tiêu đề cột
            if (_headerRow >= 0 && _rows.Count > _headerRow + 1)
            {
                var lastDataRow = _rows.FindLastIndex(row => row.Length > 0 && row[0].Style != XlsxStyle.Bold && row[0].Style != XlsxStyle.BoldInteger) + 1;
                lastDataRow = Math.Max(lastDataRow, _headerRow + 1);
                sb.Append($"<autoFilter ref=\"A{_headerRow + 1}:{ColumnName(_headerColumns - 1)}{lastDataRow}\"/>");
            }

            sb.Append("</worksheet>");
            return sb.ToString();
        }

        // 0 -> A, 25 -> Z, 26 -> AA ...
        private static string ColumnName(int index)
        {
            var name = "";
            index++;
            while (index > 0)
            {
                var mod = (index - 1) % 26;
                name = (char)('A' + mod) + name;
                index = (index - mod) / 26;
            }
            return name;
        }
    }
}
