using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QslStudio
{
    internal sealed class Qso
    {
        internal string Station, Other, Date, Utc, Frequency, Band, Mode, Rst, Downlink, Satellite, OtherGrid, Via, Note, Sent, Received;
        internal bool Swl;

        internal void CheckStation(string own)
        {
            if (Station.Length > 0 && own.Length > 0 && !Station.Equals(own, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("日志中的本台呼号为 " + Station + "，当前卡片为 " + own + "。请先切换台站资料，或只勾选「" + own + "」的记录。");
        }
        internal static void CheckBatch(Project project, IList<Qso> records)
        {
            if (records.Count == 0) throw new InvalidDataException("请勾选要导出的记录。");
            string current = project.StationCall;
            string own = current.Length == 0 ? records[0].Station : current;
            if (own.Length == 0)
                throw new InvalidDataException("请先在「本台」填写实际呼号，再批量导出。");
            foreach (var record in records)
            {
                record.CheckStation(own);
                if (current.Length == 0 && record.Station.Length == 0)
                    throw new InvalidDataException("有勾选的记录未注明本台呼号。请先在「本台」填好呼号再导出。");
            }
        }
        internal void Apply(Project project)
        {
            string own = project.StationCall;
            CheckStation(own);
            project.ClearDemoFields();
            if (own.Length == 0 && Station.Length > 0) project.Values["own"] = Station;
            foreach (var field in Field.All) if (!field.Station) project.Values[field.Key] = "";
            string back = project.TemplateOn(1);
            project.Values["other"] = Other;
            project.Values["date"] = Date;
            project.Values["utc"] = Utc;
            project.Values["mhz"] = Frequency.Length == 0 && (Templates.IsPortrait(back) || back == "callsign" || back == "handwritten" || back == "contest") ? Band : Frequency;
            project.Values["mode"] = Mode;
            project.Values["rst"] = Rst;
            project.Values["confirm"] = Swl ? "SWL" : "QSO";
            project.Values["sat"] = Satellite;
            project.Values["mhzRx"] = Downlink;
            project.Values["otherGrid"] = OtherGrid;
            project.Values["via"] = Via;
            project.Values["note"] = Note;
            project.Values["stx"] = Sent;
            project.Values["srx"] = Received;
        }
    }

    internal sealed class AdifLog
    {
        internal readonly List<Qso> Records = new List<Qso>();
        internal int Skipped;

        private static readonly HashSet<string> Columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "STATION_CALLSIGN", "CALL", "QSO_DATE", "TIME_ON", "FREQ", "BAND", "FREQ_RX", "MODE", "SUBMODE", "RST_SENT",
            "SAT_NAME", "GRIDSQUARE", "QSL_VIA", "QSLMSG", "QSLMSG_INTL", "STX_STRING", "STX", "SRX_STRING", "SRX", "SWL"
        };

        internal static AdifLog Read(string path)
        {
            if (new FileInfo(path).Length > 32L * 1024 * 1024)
                throw new InvalidDataException("此日志超过 32 MB。请在日志软件里按日期分段导出 ADIF 文件。");
            var log = new AdifLog();
            using (var reader = new StreamReader(path, Encoding.UTF8, true)) log.Read(reader);
            if (log.Records.Count == 0) throw new InvalidDataException("日志中没有找到包含对方呼号的 ADIF 记录。");
            return log;
        }

        private void Read(TextReader reader)
        {
            var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tag = new StringBuilder();
            char[] buffer = new char[4096];
            int ch;
            while ((ch = reader.Read()) != -1)
            {
                if (ch != '<') continue;
                tag.Length = 0;
                while ((ch = reader.Read()) != -1 && ch != '>' && ch != '<')
                    if (tag.Length < 256) tag.Append((char)ch);
                if (ch == '<') throw Broken(Records.Count + Skipped);
                if (ch == -1) throw Broken(Records.Count + Skipped);
                var part = tag.ToString().Trim().Split(':');
                if (part.Length == 1 && part[0].Equals("EOH", StringComparison.OrdinalIgnoreCase)) { columns.Clear(); continue; }
                if (part.Length == 1 && part[0].Equals("EOR", StringComparison.OrdinalIgnoreCase))
                {
                    AddRecord(columns);
                    columns.Clear();
                    continue;
                }
                if (part.Length < 2) continue;
                int length;
                if (!int.TryParse(part[1].Trim(), NumberStyles.None, Files.Inv, out length)) throw Broken(Records.Count + Skipped);
                string key = part[0].Trim();
                int first = Math.Min(length, buffer.Length);
                if (reader.ReadBlock(buffer, 0, first) != first) throw Broken(Records.Count + Skipped);
                if (Columns.Contains(key)) columns[key] = new string(buffer, 0, first).Trim();
                for (int left = length - first; left > 0; )
                {
                    int count = Math.Min(left, buffer.Length);
                    if (reader.ReadBlock(buffer, 0, count) != count) throw Broken(Records.Count + Skipped);
                    left -= count;
                }
            }
            if (columns.Count > 0) throw new InvalidDataException("日志末尾缺少 <EOR>。请从日志软件重新导出。");
        }

        private static InvalidDataException Broken(int before)
        {
            return new InvalidDataException("已读取 " + before + " 条记录。随后碰到不完整的 ADIF 字段，请从日志软件重新导出。");
        }

        private static string Value(Dictionary<string, string> fields, string key, int max = 120)
        {
            string value;
            return fields.TryGetValue(key, out value) ? Files.Cut(value, max) : "";
        }
        private static string First(Dictionary<string, string> fields, string primary, string fallback, int max = 120)
        {
            string value = Value(fields, primary, max);
            return value.Length > 0 ? value : Value(fields, fallback, max);
        }
        private static string Format(string input, string[] formats, string output)
        {
            DateTime result;
            return DateTime.TryParseExact(input, formats, Files.Inv, DateTimeStyles.None, out result) ? result.ToString(output, Files.Inv) : "";
        }
        private static readonly string[] DateFormats = { "yyyyMMdd" }, TimeFormats = { "HHmm", "HHmmss" };

        private void AddRecord(Dictionary<string, string> data)
        {
            string other = Value(data, "CALL", 48).ToUpperInvariant();
            if (other.Length == 0) { Skipped++; return; }
            Records.Add(new Qso {
                Station = Value(data, "STATION_CALLSIGN", 48).ToUpperInvariant(), Other = other,
                Date = Format(Value(data, "QSO_DATE"), DateFormats, "yyyy-MM-dd"),
                Utc = Format(Value(data, "TIME_ON"), TimeFormats, "HH:mm"),
                Frequency = Value(data, "FREQ", 32), Band = Value(data, "BAND", 24),
                Mode = First(data, "SUBMODE", "MODE", 32).ToUpperInvariant(), Rst = Value(data, "RST_SENT", 24),
                Downlink = Value(data, "FREQ_RX", 32), Satellite = Value(data, "SAT_NAME", 64),
                OtherGrid = Value(data, "GRIDSQUARE", 16), Via = Value(data, "QSL_VIA"),
                Note = First(data, "QSLMSG_INTL", "QSLMSG", 420), Sent = First(data, "STX_STRING", "STX"),
                Received = First(data, "SRX_STRING", "SRX"), Swl = Value(data, "SWL").Equals("Y", StringComparison.OrdinalIgnoreCase)
            });
        }
    }
}
