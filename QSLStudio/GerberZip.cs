using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace QslStudio
{
    // .NET 4.0 predates ZipArchive. Write ordinary ZIP entries without buffering an entire Gerber layer.
    internal sealed class GerberZip
    {
        private static readonly Encoding TextEncoding = new UTF8Encoding(false);
        private readonly BinaryWriter binary;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly ushort dosDate, dosTime;

        private sealed class Entry
        {
            internal byte[] Name;
            internal uint Start, Crc, Compressed, Uncompressed;
        }

        internal GerberZip(Stream output)
        {
            binary = new BinaryWriter(output, TextEncoding);
            DateTime saved = DateTime.Now;
            if (saved.Year < 1980) saved = new DateTime(1980, 1, 1);
            if (saved.Year > 2107) saved = new DateTime(2107, 12, 31, 23, 59, 58);
            dosDate = (ushort)(((saved.Year - 1980) << 9) | (saved.Month << 5) | saved.Day);
            dosTime = (ushort)((saved.Hour << 11) | (saved.Minute << 5) | (saved.Second / 2));
        }

        internal void Add(string name, Action<TextWriter> write)
        {
            AddBytes(name, content =>
            {
                using (var text = new StreamWriter(content, TextEncoding, 4096))
                {
                    text.NewLine = "\n";
                    write(text);
                }
            });
        }

        internal void AddBytes(string name, Action<Stream> write)
        {
            Stream stream = binary.BaseStream;
            var entry = new Entry { Name = Encoding.ASCII.GetBytes(name), Start = checked((uint)stream.Position) };
            binary.Write(0x04034b50u);
            binary.Write((ushort)20);
            WriteEntryFields(entry);
            binary.Write(entry.Name);
            long compressedStart = stream.Position;
            using (var compressor = new DeflateStream(stream, CompressionMode.Compress, true))
            using (var content = new CheckedStream(compressor))
            {
                write(content);
                entry.Crc = content.Crc;
                entry.Uncompressed = checked((uint)content.Length);
            }
            long end = stream.Position;
            entry.Compressed = checked((uint)(end - compressedStart));
            stream.Position = entry.Start + 14;
            binary.Write(entry.Crc);
            binary.Write(entry.Compressed);
            binary.Write(entry.Uncompressed);
            stream.Position = end;
            entries.Add(entry);
        }

        private void WriteEntryFields(Entry e)
        {
            binary.Write((ushort)0); // Flags
            binary.Write((ushort)8); // Deflate
            binary.Write(dosTime);
            binary.Write(dosDate);
            binary.Write(e.Crc);
            binary.Write(e.Compressed);
            binary.Write(e.Uncompressed);
            binary.Write((ushort)e.Name.Length);
            binary.Write((ushort)0); // Extra field length
        }

        internal void Finish()
        {
            uint start = checked((uint)binary.BaseStream.Position);
            foreach (var e in entries)
            {
                binary.Write(0x02014b50u);
                binary.Write((ushort)20);
                binary.Write((ushort)20);
                WriteEntryFields(e);
                binary.Write((ushort)0); // Comment length
                binary.Write((ushort)0); // Disk number
                binary.Write((ushort)0); // Internal attributes
                binary.Write(0u); // External attributes
                binary.Write(e.Start);
                binary.Write(e.Name);
            }
            uint length = checked((uint)binary.BaseStream.Position - start);
            binary.Write(0x06054b50u);
            binary.Write((ushort)0);
            binary.Write((ushort)0);
            binary.Write((ushort)entries.Count);
            binary.Write((ushort)entries.Count);
            binary.Write(length);
            binary.Write(start);
            binary.Write((ushort)0);
            binary.Flush();
        }

        private sealed class CheckedStream : Stream
        {
            private static readonly uint[] Table = MakeCrcTable();
            private readonly Stream output;
            private long length;
            private uint crc = 0xffffffffu;
            internal CheckedStream(Stream output) { this.output = output; }
            internal uint Crc { get { return ~crc; } }
            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { return length; } }
            public override long Position { get { return length; } set { throw new NotSupportedException(); } }
            public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Flush() { output.Flush(); }
            public override void Write(byte[] buffer, int offset, int count)
            {
                uint value = crc;
                for (int i = offset; i < offset + count; i++) value = Table[(value ^ buffer[i]) & 255] ^ (value >> 8);
                crc = value;
                output.Write(buffer, offset, count);
                length += count;
            }
            private static uint[] MakeCrcTable()
            {
                var table = new uint[256];
                for (uint i = 0; i < table.Length; i++)
                {
                    uint c = i;
                    for (int bit = 0; bit < 8; bit++) c = (c & 1) != 0 ? (c >> 1) ^ 0xedb88320u : c >> 1;
                    table[i] = c;
                }
                return table;
            }
        }
    }
}
