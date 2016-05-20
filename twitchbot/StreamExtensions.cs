using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace twitchbot
{
    public static class StreamExtensions
    {
        public static void WriteInt(this Stream stream, int value)
        {
            var bytes = BitConverter.GetBytes(value);

            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 24)));
        }

        public static void WriteLong(this Stream stream, long value)
        {
            var bytes = BitConverter.GetBytes(value);

            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 32) & 0xFF));
            stream.WriteByte((byte)((value >> 40) & 0xFF));
            stream.WriteByte((byte)((value >> 48) & 0xFF));
            stream.WriteByte((byte)((value >> 56) & 0xFF));
        }

        public static void WriteString(this Stream stream, string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            int length = bytes.Length;
            stream.WriteByte((byte)(length & 0xFF));
            stream.WriteByte((byte)(length >> 8));
            stream.Write(bytes, 0, length);
        }

        public static string ReadString(this Stream stream)
        {
            int length = stream.ReadByte() | (stream.ReadByte() << 8);
            byte[] buffer = new byte[length];
            stream.Read(buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        public static int ReadInt(this Stream stream)
        {
            return unchecked((int)(stream.ReadByte() | (stream.ReadByte() << 8) | (stream.ReadByte() << 16) | (stream.ReadByte() << 24)));
        }

        public static long ReadLong(this Stream stream)
        {
            return (((long)stream.ReadByte())) | (((long)stream.ReadByte()) << 8) | (((long)stream.ReadByte()) << 16) | (((long)stream.ReadByte()) << 24)
                 | (((long)stream.ReadByte()) << 32) | (((long)stream.ReadByte()) << 40) | (((long)stream.ReadByte()) << 48) | (((long)stream.ReadByte()) << 56);
        }
    }
}
