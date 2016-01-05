using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Roslyn.Utilities;

namespace Mono.CompilerServices.SymbolWriter
{
    internal class UnsafeComStreamWrapper : Stream
    {
        private static readonly FieldInfo PositionFieldInfo;
        private static readonly FieldInfo LengthFieldInfo;

        private readonly IUnsafeComStream _stream;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => (int)LengthFieldInfo.GetValue(_stream);
        public override long Position
        {
            get { return (int)PositionFieldInfo.GetValue(_stream); }
            set { Seek(value, SeekOrigin.Begin); }
        }

        static UnsafeComStreamWrapper()
        {
            PositionFieldInfo = typeof(ComMemoryStream).GetRuntimeFields().First(f => f.Name == "_position");
            LengthFieldInfo = typeof(ComMemoryStream).GetRuntimeFields().First(f => f.Name == "_length");
        }

        public UnsafeComStreamWrapper(ComMemoryStream stream)
        {
            _stream = stream;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosPtr = Marshal.AllocHGlobal(8);
            _stream.Seek(offset, (int)origin, newPosPtr);
            var newPos = Marshal.ReadInt64(newPosPtr);
            Marshal.FreeHGlobal(newPosPtr);
            return newPos;
        }

        public override void SetLength(long value)
        {
            _stream.SetSize(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var bufferPtr = pinnedBuffer.AddrOfPinnedObject();
            var readPtr = Marshal.AllocHGlobal(8);

            _stream.Read(bufferPtr + offset, count, readPtr);
            var read = Marshal.ReadInt64(readPtr);

            pinnedBuffer.Free();
            Marshal.FreeHGlobal(readPtr);

            return (int)read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var bufferPtr = pinnedBuffer.AddrOfPinnedObject();
            var sizePtr = Marshal.AllocHGlobal(8);

            _stream.Write(bufferPtr + offset, count, sizePtr);

            pinnedBuffer.Free();
            Marshal.FreeHGlobal(sizePtr);
        }
    }
}
