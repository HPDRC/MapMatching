using System;
using System.IO;
using System.Text;

namespace MapMatchingLib.SysTools
{
    public class StreamString
    {
        private readonly Stream _ioStream;
        private readonly UnicodeEncoding _streamEncoding;

        public StreamString(Stream ioStream)
        {
            _ioStream = ioStream;
            _streamEncoding = new UnicodeEncoding();
        }

        public string ReadString()
        {
            byte[] lenByte=new byte[4];
            _ioStream.Read(lenByte, 0, 4);
            int len = BitConverter.ToInt32(lenByte, 0);
            //int len = _ioStream.ReadByte() * 256;
            //len += _ioStream.ReadByte();
            byte[] inBuffer = new byte[len];
            _ioStream.Read(inBuffer, 0, len);
            return _streamEncoding.GetString(inBuffer);
        }

        public int WriteString(string outString)
        {
            byte[] outBuffer = _streamEncoding.GetBytes(outString);
            int len = outBuffer.Length;
            byte[] lenByte = BitConverter.GetBytes(len);
            _ioStream.Write(lenByte, 0, 4);
            _ioStream.Write(outBuffer, 0, len);
            _ioStream.Flush();
            return outBuffer.Length + 4;
        }
    }
}
