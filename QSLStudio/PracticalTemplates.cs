using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace QslStudio
{
    internal static partial class Templates
    {
        private static void MakePractical(Project p)
        {
            Color ink = C(0x192d38), gray = C(0x495965);
            switch (p.Template)
            {
                case "postal":
                    MakeFullBleed(p);
                    PostalBack(p, ink, gray);
                    break;
                case "label":
                    TypeFront(p, "QSL CARD", "AMATEUR RADIO STATION", ink);
                    LabelBack(p, ink, gray);
                    break;
                case "satellite":
                    p.Background[0] = C(0xe8edf0);
                    AddCover(p);
                    SplitFront(p, 62, 610, "SATELLITE QSO", "卫星通联确认卡", "grid", ink);
                    SatelliteBack(p, ink, gray);
                    break;
                case "swl":
                    CenteredSwlFront(p, ink);
                    SwlBack(p, ink, gray);
                    break;
                case "eyeball":
                    p.Background[0] = C(0xf5efe1);
                    AddCover(p);
                    SplitFront(p, 813, 533, "EYEBALL QSO", "见面交换纪念", "activity", C(0x663c2a));
                    EyeballBack(p, C(0x663c2a), C(0x52483e));
                    break;
                case "callsign":
                    CallsignFront(p);
                    CommonBack(p, ink, gray);
                    break;
                case "handwritten":
                    p.Background[0] = C(0xf6f3eb);
                    AddCover(p);
                    SplitFront(p, 655, 654, "QSL CARD", "业余无线电通联确认", "operator", C(0x302f2a));
                    HandwrittenBack(p, ink, gray);
                    break;
                case "contest":
                    ContestFront(p);
                    ContestBack(p, ink, gray);
                    break;
                case "postal-cn":
                    MakeFullBleed(p);
                    DomesticPostalBack(p, ink, gray);
                    break;
                case "open-note":
                    CallsignFront(p);
                    OpenNoteBack(p, ink, gray);
                    break;
            }
        }

        private static void CallsignFront(Project p)
        {
            Color ink = C(0x202722), accent = C(0x305745);
            p.Background[0] = C(0xf8f7f1);
            Text(p, 0, "卡片标题", "业余无线电台   AMATEUR RADIO STATION", 78, 123, 1242, 64, 13, accent, true);
            Add(p, 0, "own", 71, 254, 1255, 277, 71, ink, true);
            Add(p, 0, "location", 81, 615, 981, 63, 16, ink, true);
            Add(p, 0, "operator", 81, 692, 981, 48, 11, ink, false);
            Add(p, 0, "grid", 1090, 617, 219, 59, 16, accent, true);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Far;
        }

        private static void ContestFront(Project p)
        {
            Color dark = C(0x182a39), paper = C(0xf7f7f3);
            p.Background[0] = paper;
            Text(p, 0, "卡片标题", "CONTEST QSL", 74, 94, 1180, 62, 15, Color.White, true);
            Add(p, 0, "activity", 74, 181, 1240, 109, 26, Color.White, true);
            Add(p, 0, "own", 71, 364, 1242, 209, 58, dark, true);
            Add(p, 0, "location", 79, 633, 1246, 60, 16, dark, true);
            Add(p, 0, "reference", 79, 715, 670, 51, 11, dark, false);
            Add(p, 0, "operator", 806, 715, 515, 51, 11, dark, false);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Far;
        }

        private static void BackTo(Project p, Color ink, Color gray, int y)
        {
            Text(p, 1, "收卡栏名", "TO RADIO / 对方呼号", 72, y, 660, 37, 9, gray, true);
            Add(p, 1, "other", 70, y + 38, 660, 80, 21, ink, true);
        }

        private static void CommonBack(Project p, Color ink, Color gray)
        {
            BackHead(p, "QSL CONFIRMATION / 通联确认", ink, gray, 820);
            SmallField(p, "GRID", "grid", 943, 119, 383, gray, ink, 96);
            SmallField(p, "QSL VIA", "via", 943, 175, 383, gray, ink, 137);
            BackTo(p, ink, gray, 279);
            Check(p, "confirm", "QSO", "通联勾选框", 823, 279, ink);
            Check(p, "confirm", "SWL", "收听勾选框", 823, 325, ink);
            Check(p, "confirm", "EYEBALL", "见面交换勾选框", 823, 371, ink);
            Text(p, 1, "确认通联", "OUR QSO", 857, 269, 460, 43, 9.5f, ink, false);
            Text(p, 1, "确认收听", "YOUR SWL REPORT", 857, 315, 460, 43, 9.5f, ink, false);
            Text(p, 1, "确认见面", "EYEBALL QSO", 857, 361, 460, 43, 9.5f, ink, false);
            QsoCells(p, StandardHeadings, StandardFields, CommonColumns, 435, 477, 12, ink, gray);
            SmallField(p, "WKD", "worked", 73, 578, 583, gray, ink, 100);
            SmallField(p, "PLACE", "venue", 708, 578, 615, gray, ink, 111);
            Text(p, 1, "备注栏名", "REMARKS / 留言", 73, 645, 1110, 33, 8, gray, true);
            Add(p, 1, "note", 73, 684, 1250, 67, 10, ink, false);
            SmallField(p, "QSL", "reply", 73, 786, 540, gray, ink, 92);
            SmallField(p, "SIGNED", "signature", 778, 786, 545, gray, ink, 138);
        }

        private static void HandwrittenBack(Project p, Color ink, Color gray)
        {
            BackHead(p, "AMATEUR RADIO / 业余无线电", ink, gray, 1200);
            Text(p, 1, "确认短句", "Confirming our QSO with", 72, 275, 486, 61, 12, ink, false);
            Add(p, 1, "other", 573, 267, 746, 70, 17, ink, true);
            QsoCells(p, StandardHeadings, StandardFields, CommonColumns, 390, 446, 12, ink, gray);
            Text(p, 1, "留言栏名", "Remarks / 留言", 72, 566, 1234, 37, 9, gray, false);
            Add(p, 1, "note", 73, 615, 1245, 100, 11, ink, false);
            SmallField(p, "QSL VIA", "via", 72, 775, 610, gray, ink, 130);
            SmallField(p, "SIGNED", "signature", 770, 775, 549, gray, ink, 137);
        }

        private static void ContestBack(Project p, Color ink, Color gray)
        {
            BackHead(p, "CONTEST QSO / 赛事通联", ink, gray, 710);
            Add(p, 1, "activity", 816, 81, 513, 82, 13, ink, true);
            SmallField(p, "OP", "operator", 817, 177, 510, gray, ink, 80);
            BackTo(p, ink, gray, 267);
            SmallField(p, "REF.", "reference", 817, 308, 510, gray, ink, 92);
            QsoCells(p, ContestHeadings, ContestFields, ContestColumns, 425, 469, 11, ink, gray);
            Text(p, 1, "备注栏名", "REMARKS / 留言", 72, 581, 1234, 38, 9, gray, true);
            Add(p, 1, "note", 72, 626, 1245, 105, 11, ink, false);
            SmallField(p, "QSL VIA", "via", 72, 773, 610, gray, ink, 129);
            SmallField(p, "SIGNED", "signature", 770, 773, 549, gray, ink, 134);
        }

        private static void TypeFront(Project p, string title, string subtitle, Color ink)
        {
            Text(p, 0, "主题", title, 69, 95, 1255, 55, 16, ink, true);
            Text(p, 0, "副标题", subtitle, 71, 160, 1200, 41, 10.2f, ink, false);
            Add(p, 0, "own", 59, 257, 1277, 248, 64, ink, true);
            Add(p, 0, "location", 74, 576, 1230, 60, 16, ink, true);
            Add(p, 0, "operator", 75, 659, 880, 47, 11, ink, false);
        }

        private static void SplitFront(Project p, int x, int w, string title, string subtitle, string lastField, Color ink)
        {
            Text(p, 0, "主题", title, x, 151, w, 58, 14, ink, true);
            Text(p, 0, "副标题", subtitle, x, 209, w, 46, 10.4f, ink, false);
            Add(p, 0, "own", x - 3, 339, w + 3, 152, 42, ink, true);
            Add(p, 0, "location", x, 569, w, 53, 12, ink, true);
            Add(p, 0, lastField, x, 632, w, 43, 10.4f, ink, false);
        }

        private static void CenteredSwlFront(Project p, Color ink)
        {
            Text(p, 0, "主题", "SHORTWAVE LISTENER CONFIRMATION", 60, 117, 1280, 48, 14, ink, true);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
            Text(p, 0, "副标题", "业余电台收听确认", 60, 174, 1280, 43, 11, ink, false);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
            Add(p, 0, "own", 60, 278, 1280, 224, 60, ink, true);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
            Add(p, 0, "location", 64, 584, 1272, 62, 15, ink, true);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
        }

        private static void BackHead(Project p, string heading, Color ink, Color gray, int width)
        {
            Text(p, 1, "确认类型", heading, 62, 49, width, 43, 10.5f, gray, true);
            Add(p, 1, "own", 59, 93, width, 109, 29, ink, true);
            Add(p, 1, "location", 63, 204, width, 39, 9.4f, ink, false);
        }

        private static void PostalBack(Project p, Color ink, Color gray)
        {
            BackHead(p, "QSL CONFIRMATION / 通联确认", ink, gray, 635);
            Text(p, 1, "收卡栏名", "TO RADIO / 对方呼号", 62, 257, 624, 37, 8.8f, gray, true);
            Add(p, 1, "other", 62, 301, 624, 79, 19, ink, true);
            SmallField(p, "DATE", "date", 64, 408, 322, gray, ink, 89);
            SmallField(p, "UTC", "utc", 399, 408, 266, gray, ink, 72);
            SmallField(p, "MHz", "mhz", 64, 463, 322, gray, ink, 89);
            SmallField(p, "MODE", "mode", 399, 463, 267, gray, ink, 84);
            SmallField(p, "RST", "rst", 64, 520, 234, gray, ink, 89);
            SmallField(p, "QSL", "reply", 322, 520, 338, gray, ink, 76);
            SmallField(p, "FROM / 寄件", "address", 65, 658, 597, gray, ink, 165);
            SmallField(p, "签名 / SIGN", "signature", 65, 709, 591, gray, ink, 164);
            Text(p, 1, "邮票位", "邮票 / STAMP", 1094, 96, 236, 38, 7.5f, gray, false);
            p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
            Text(p, 1, "收件栏名", "收件人及地址 / TO", 765, 325, 561, 49, 11, ink, true);
            Add(p, 1, "recipient", 765, 410, 568, 235, 12, ink, false);
        }

        private static void DomesticPostalBack(Project p, Color ink, Color gray)
        {
            Add(p, 1, "postcode", 64, 60, 336, 52, 10, C(0x9e3a30), true);
            p.Layers[p.Layers.Count - 1].Symbol = "postcode";
            Text(p, 1, "台站标题", "中国业余电台 / AMATEUR RADIO STATION", 64, 151, 626, 41, 8.7f, gray, true);
            Add(p, 1, "own", 61, 190, 625, 110, 29, ink, true);
            Add(p, 1, "location", 65, 302, 605, 37, 8.5f, gray, false);
            SmallField(p, "TO RADIO", "other", 64, 372, 613, gray, ink, 158);
            SmallField(p, "DATE", "date", 64, 450, 334, gray, ink, 74);
            SmallField(p, "UTC", "utc", 428, 450, 251, gray, ink, 60);
            SmallField(p, "MHz", "mhz", 64, 506, 334, gray, ink, 74);
            SmallField(p, "MODE", "mode", 428, 506, 251, gray, ink, 80);
            SmallField(p, "RST", "rst", 64, 563, 256, gray, ink, 74);
            SmallField(p, "QSL", "reply", 365, 563, 314, gray, ink, 65);
            Text(p, 1, "寄件栏名", "寄件地址 / FROM", 64, 650, 611, 36, 7.5f, gray, true);
            Add(p, 1, "address", 64, 687, 613, 58, 8, ink, false);
            p.Layers[p.Layers.Count - 1].Symbol = "paragraph";
            SmallField(p, "EMAIL", "email", 64, 748, 613, gray, ink, 93);
            SmallField(p, "SIGN", "signature", 64, 786, 613, gray, ink, 78);
            Text(p, 1, "邮票位", "贴邮票", 1064, 60, 265, 297, 8.2f, gray, false);
            Layer stamp = p.Layers[p.Layers.Count - 1];
            stamp.Symbol = "stampbox";
            stamp.Align = StringAlignment.Center;
            Text(p, 1, "收件栏名", "收件人及地址", 766, 408, 563, 45, 10, gray, true);
            Add(p, 1, "recipient", 766, 478, 563, 260, 11, ink, false);
            p.Layers[p.Layers.Count - 1].Symbol = "mailaddress";
        }

        private static void OpenNoteBack(Project p, Color ink, Color gray)
        {
            BackHead(p, "QSL CONFIRMATION / 通联确认", ink, gray, 825);
            SmallField(p, "GRID", "grid", 961, 126, 363, gray, ink, 96);
            BackTo(p, ink, gray, 263);
            QsoCells(p, StandardHeadings, StandardFields, CommonColumns, 388, 421, 11, ink, gray);
            Text(p, 1, "留言栏名", "留言 / REMARKS", 73, 513, 1225, 38, 8.5f, gray, true);
            Add(p, 1, "note", 73, 559, 1251, 195, 11, ink, false);
            p.Layers[p.Layers.Count - 1].Symbol = "paragraph";
            SmallField(p, "QSL", "reply", 73, 793, 535, gray, ink, 92);
            SmallField(p, "SIGN", "signature", 818, 793, 506, gray, ink, 104);
        }

        private static void LabelBack(Project p, Color ink, Color gray)
        {
            BackHead(p, "QSL CONFIRMATION / 通联确认", ink, gray, 820);
            SmallField(p, "GRID", "grid", 918, 70, 409, gray, ink, 104);
            SmallField(p, "QSL VIA", "via", 918, 121, 409, gray, ink, 137);
            SmallField(p, "TO RADIO", "other", 63, 274, 720, gray, ink, 151);
            QsoCells(p, new[] { "DATE / 日期", "UTC", "MHz", "MODE", "RST" },
                new[] { "date", "utc", "mhz", "mode", "rst" }, LabelColumns, 728, 766, 8.8f, ink, gray);
            SmallField(p, "SIGN", "signature", 1011, 733, 314, gray, ink, 99);
            SmallField(p, "备注", "note", 1011, 778, 314, gray, ink, 75);
        }

        private static void SatelliteBack(Project p, Color ink, Color gray)
        {
            BackHead(p, "SATELLITE QSO / 卫星通联", ink, gray, 750);
            SmallField(p, "MY GRID", "grid", 65, 270, 536, gray, ink, 142);
            Text(p, 1, "对方呼号栏名", "TO RADIO / 对方呼号", 820, 61, 510, 40, 9.2f, gray, true);
            Add(p, 1, "other", 820, 113, 514, 105, 24, ink, true);
            SmallField(p, "YOUR GRID", "otherGrid", 820, 270, 514, gray, ink, 178);
            Text(p, 1, "卫星栏名", "SAT / 卫星", 65, 337, 180, 57, 10, gray, true);
            Add(p, 1, "sat", 254, 335, 1046, 62, 16, ink, true);
            QsoCells(p, new[] { "DATE", "UTC", "UP / MHz", "DOWN / MHz", "MODE", "RST" },
                new[] { "date", "utc", "mhz", "mhzRx", "mode", "rst" }, SatelliteColumns, 441, 520, 12, ink, gray);
            SmallField(p, "QSL VIA", "via", 63, 665, 578, gray, ink, 143);
            SmallField(p, "SIGN", "signature", 805, 665, 511, gray, ink, 109);
            SmallField(p, "备注", "note", 63, 744, 1221, gray, ink, 116);
        }

        private static void SwlBack(Project p, Color ink, Color gray)
        {
            BackHead(p, "SWL RECEPTION CONFIRMED / 收听确认", ink, gray, 1200);
            Text(p, 1, "听者栏名", "TO SWL / 收听者呼号", 69, 273, 593, 41, 9, gray, true);
            Text(p, 1, "通联对象栏名", "WKD / 通联中的另一台", 728, 273, 599, 41, 9, gray, true);
            Add(p, 1, "other", 69, 318, 593, 88, 21, ink, true);
            Add(p, 1, "worked", 728, 318, 599, 88, 21, ink, true);
            QsoCells(p, new[] { "DATE / 日期", "UTC", "MHz", "MODE", "RST" },
                new[] { "date", "utc", "mhz", "mode", "rst" }, SwlColumns, 465, 534, 12, ink, gray);
            SmallField(p, "REMARKS", "note", 63, 677, 1245, gray, ink, 167);
            SmallField(p, "SIGN", "signature", 63, 746, 670, gray, ink, 103);
        }

        private static void EyeballBack(Project p, Color ink, Color gray)
        {
            BackHead(p, "EYEBALL QSO / 见面交换", ink, gray, 1200);
            Text(p, 1, "对方栏名", "TO / 对方呼号", 63, 285, 600, 36, 9.7f, gray, true);
            Add(p, 1, "other", 61, 333, 1259, 111, 25, ink, true);
            SmallField(p, "日期 / DATE", "date", 64, 504, 516, gray, ink, 176);
            SmallField(p, "UTC", "utc", 680, 504, 619, gray, ink, 110);
            SmallField(p, "见面地点 / PLACE", "venue", 64, 570, 1271, gray, ink, 236);
            SmallField(p, "活动 / EVENT", "activity", 64, 636, 1271, gray, ink, 206);
            SmallField(p, "签名 / SIGN", "signature", 64, 725, 1271, gray, ink, 201);
        }

        internal static readonly int[] LabelColumns = { 62, 300, 465, 650, 816, 982 };
        internal static readonly int[] SatelliteColumns = { 61, 309, 477, 685, 893, 1110, 1339 };
        internal static readonly int[] SwlColumns = { 61, 366, 568, 812, 1063, 1339 };
        internal static readonly int[] CommonColumns = { 72, 363, 559, 869, 1104, 1328 };
        internal static readonly int[] ContestColumns = { 72, 310, 457, 671, 824, 949, 1137, 1328 };
        private static readonly string[] StandardHeadings = { "DATE (UTC)", "TIME (UTC)", "MHz / BAND", "MODE", "RST" };
        private static readonly string[] StandardFields = { "date", "utc", "mhz", "mode", "rst" };
        private static readonly string[] ContestHeadings = { "DATE (UTC)", "TIME (UTC)", "MHz / BAND", "MODE", "RST", "SENT", "RCVD" };
        private static readonly string[] ContestFields = { "date", "utc", "mhz", "mode", "rst", "stx", "srx" };

        private static void QsoCells(Project p, string[] labels, string[] fields, int[] columns,
            int headingY, int dataY, float size, Color ink, Color gray)
        {
            for (int col = 0; col < fields.Length; col++)
            {
                int x = columns[col] + 8, w = columns[col + 1] - columns[col] - 16;
                Text(p, 1, labels[col] + " 栏名", labels[col], x, headingY, w, 35, 8.5f, gray, true);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
                Add(p, 1, fields[col], x, dataY, w, 62, size, ink, false);
                p.Layers[p.Layers.Count - 1].Align = StringAlignment.Center;
            }
        }
    }

    internal static partial class CardPainter
    {
        private static void DrawPracticalRules(Graphics g, string key, int side, bool blankNote)
        {
            using (var pen = new Pen(Color.FromArgb(117, 129, 133), 1.7f))
            {
                if (side == 0)
                {
                    if (key == "satellite") Fill(g, Color.FromArgb(208, 219, 226), 705, 0, 695, 900);
                    else if (key == "eyeball") Fill(g, Color.FromArgb(229, 221, 203), 0, 0, 760, 900);
                    else if (key == "swl") g.DrawRectangle(pen, 42, 42, 1316, 808);
                    else if (key == "label")
                    {
                        g.DrawLine(pen, 63, 225, 1337, 225);
                        g.DrawLine(pen, 63, 536, 1337, 536);
                    }
                    else if (key == "callsign" || key == "open-note")
                    {
                        Fill(g, Color.FromArgb(48, 87, 69), 0, 0, 1400, 54);
                        g.DrawLine(pen, 78, 576, 1309, 576);
                    }
                    else if (key == "handwritten")
                        Fill(g, Color.FromArgb(226, 223, 213), 61, 59, 536, 756);
                    else if (key == "contest")
                        Fill(g, Color.FromArgb(29, 49, 65), 0, 0, 1400, 326);
                    return;
                }
                switch (key)
                {
                    case "callsign":
                        g.DrawLine(pen, 70, 242, 1328, 242);
                        DrawRecords(g, pen, Templates.CommonColumns, 425, 467, 550);
                        g.DrawLine(pen, 175, 619, 652, 619);
                        g.DrawLine(pen, 820, 619, 1328, 619);
                        g.DrawLine(pen, 72, 759, 1328, 759);
                        break;
                    case "handwritten":
                        g.DrawLine(pen, 564, 340, 1328, 340);
                        for (int i = 0; i < Templates.CommonColumns.Length - 1; i++)
                            g.DrawLine(pen, Templates.CommonColumns[i] + 8, 515, Templates.CommonColumns[i + 1] - 8, 515);
                        if (blankNote) g.DrawLine(pen, 72, 669, 1328, 669);
                        g.DrawLine(pen, 72, 727, 1328, 727);
                        g.DrawLine(pen, 207, 816, 660, 816);
                        g.DrawLine(pen, 904, 816, 1328, 816);
                        break;
                    case "contest":
                        g.DrawLine(pen, 72, 244, 1328, 244);
                        DrawRecords(g, pen, Templates.ContestColumns, 414, 459, 543);
                        g.DrawLine(pen, 72, 740, 1328, 740);
                        break;
                    case "postal":
                        g.DrawLine(pen, 720, 51, 720, 823);
                        g.DrawRectangle(pen, 1093, 55, 238, 269);
                        g.DrawLine(pen, 62, 394, 662, 394);
                        g.DrawLine(pen, 62, 576, 662, 576);
                        break;
                    case "postal-cn":
                        g.DrawLine(pen, 716, 181, 716, 819);
                        g.DrawLine(pen, 64, 428, 677, 428);
                        g.DrawLine(pen, 64, 626, 677, 626);
                        break;
                    case "open-note":
                        g.DrawLine(pen, 72, 241, 1328, 241);
                        g.DrawLine(pen, 72, 484, 1328, 484);
                        g.DrawLine(pen, 72, 770, 1328, 770);
                        break;
                    case "label":
                        pen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(pen, 61, 344, 922, 360);
                        pen.DashStyle = DashStyle.Solid;
                        g.DrawLine(pen, 62, 244, 1338, 244);
                        break;
                    case "satellite":
                        g.DrawLine(pen, 62, 312, 1338, 312);
                        DrawRecords(g, pen, Templates.SatelliteColumns, 427, 494, 617);
                        break;
                    case "swl":
                        g.DrawLine(pen, 62, 246, 1338, 246);
                        g.DrawLine(pen, 701, 268, 701, 415);
                        DrawRecords(g, pen, Templates.SwlColumns, 449, 509, 622);
                        break;
                    case "eyeball":
                        g.DrawLine(pen, 62, 245, 1338, 245);
                        for (int y = 546; y <= 678; y += 66) g.DrawLine(pen, 63, y, 1336, y);
                        g.DrawLine(pen, 63, 778, 1336, 778);
                        break;
                }
            }
        }

        private static void DrawRecords(Graphics g, Pen pen, int[] columns, int top, int header, int bottom)
        {
            int start = columns[0], end = columns[columns.Length - 1];
            g.DrawRectangle(pen, start, top, end - start, bottom - top);
            g.DrawLine(pen, start, header, end, header);
            GridLines(g, pen, columns, top, bottom);
        }

        private static void DrawPostalCode(Graphics g, Layer layer, string digits, bool guides)
        {
            float pitch = layer.W / Field.PostcodeDigits;
            float gap = Math.Min(pitch * .15f, 7);
            var digit = layer.Copy();
            digit.Symbol = "";
            digit.Align = StringAlignment.Center;
            digit.W = pitch - gap;
            using (var pen = new Pen(layer.Color, 1.8f))
                for (int i = 0; i < (guides ? Field.PostcodeDigits : digits.Length); i++)
                {
                    digit.X = layer.X + i * pitch + gap / 2;
                    if (guides) g.DrawRectangle(pen, digit.X, digit.Y, digit.W, digit.H);
                    if (i < digits.Length) DrawText(g, digit, digits[i].ToString());
                }
        }

        private static void DrawStampBox(Graphics g, Layer layer)
        {
            using (var pen = new Pen(layer.Color, 1.8f))
                g.DrawRectangle(pen, layer.X, layer.Y, layer.W, layer.H);
        }

        private static void DrawAddressLines(Graphics g, Layer layer)
        {
            using (var pen = new Pen(layer.Color, 1.3f))
                for (int line = 1; line <= 3; line++)
                {
                    float y = layer.Y + layer.H * line / 4f;
                    g.DrawLine(pen, layer.X, y, layer.X + layer.W, y);
                }
        }
    }
}
