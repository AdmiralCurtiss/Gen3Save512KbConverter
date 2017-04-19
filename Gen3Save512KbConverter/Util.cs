using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace HyoutaTools {
    public static class Util {
        #region StreamUtils
        public static void CopyStream( System.IO.Stream input, System.IO.Stream output, int count ) {
            byte[] buffer = new byte[4096];
            int read;

            int bytesLeft = count;
            while ( ( read = input.Read( buffer, 0, Math.Min( buffer.Length, bytesLeft ) ) ) > 0 ) {
                output.Write( buffer, 0, read );
                bytesLeft -= read;
                if ( bytesLeft <= 0 )
                    return;
            }
        }

        public static ulong ReadUInt64( this Stream s ) {
            ulong b1 = (ulong)s.ReadByte();
            ulong b2 = (ulong)s.ReadByte();
            ulong b3 = (ulong)s.ReadByte();
            ulong b4 = (ulong)s.ReadByte();
            ulong b5 = (ulong)s.ReadByte();
            ulong b6 = (ulong)s.ReadByte();
            ulong b7 = (ulong)s.ReadByte();
            ulong b8 = (ulong)s.ReadByte();

            return (ulong)( b8 << 56 | b7 << 48 | b6 << 40 | b5 << 32 | b4 << 24 | b3 << 16 | b2 << 8 | b1 );
        }
        public static ulong PeekUInt64( this Stream s ) {
            long pos = s.Position;
            ulong retval = s.ReadUInt64();
            s.Position = pos;
            return retval;
        }
        public static void WriteUInt64( this Stream s, ulong num ) {
            s.Write( BitConverter.GetBytes( num ), 0, 8 );
        }
        public static ulong ReadUInt56( this Stream s ) {
            ulong b1 = (ulong)s.ReadByte();
            ulong b2 = (ulong)s.ReadByte();
            ulong b3 = (ulong)s.ReadByte();
            ulong b4 = (ulong)s.ReadByte();
            ulong b5 = (ulong)s.ReadByte();
            ulong b6 = (ulong)s.ReadByte();
            ulong b7 = (ulong)s.ReadByte();

            return (ulong)( b7 << 48 | b6 << 40 | b5 << 32 | b4 << 24 | b3 << 16 | b2 << 8 | b1 );
        }
        public static ulong PeekUInt56( this Stream s ) {
            long pos = s.Position;
            ulong retval = s.ReadUInt56();
            s.Position = pos;
            return retval;
        }
        public static ulong ReadUInt48( this Stream s ) {
            ulong b1 = (ulong)s.ReadByte();
            ulong b2 = (ulong)s.ReadByte();
            ulong b3 = (ulong)s.ReadByte();
            ulong b4 = (ulong)s.ReadByte();
            ulong b5 = (ulong)s.ReadByte();
            ulong b6 = (ulong)s.ReadByte();

            return (ulong)( b6 << 40 | b5 << 32 | b4 << 24 | b3 << 16 | b2 << 8 | b1 );
        }
        public static ulong PeekUInt48( this Stream s ) {
            long pos = s.Position;
            ulong retval = s.ReadUInt48();
            s.Position = pos;
            return retval;
        }
        public static ulong ReadUInt40( this Stream s ) {
            ulong b1 = (ulong)s.ReadByte();
            ulong b2 = (ulong)s.ReadByte();
            ulong b3 = (ulong)s.ReadByte();
            ulong b4 = (ulong)s.ReadByte();
            ulong b5 = (ulong)s.ReadByte();

            return (ulong)( b5 << 32 | b4 << 24 | b3 << 16 | b2 << 8 | b1 );
        }
        public static ulong PeekUInt40( this Stream s ) {
            long pos = s.Position;
            ulong retval = s.ReadUInt40();
            s.Position = pos;
            return retval;
        }
        public static uint ReadUInt32( this Stream s ) {
            int b1 = s.ReadByte();
            int b2 = s.ReadByte();
            int b3 = s.ReadByte();
            int b4 = s.ReadByte();

            return (uint)( b4 << 24 | b3 << 16 | b2 << 8 | b1 );
        }
        public static uint PeekUInt32( this Stream s ) {
            long pos = s.Position;
            uint retval = s.ReadUInt32();
            s.Position = pos;
            return retval;
        }
        public static void WriteUInt32( this Stream s, uint num ) {
            s.Write( BitConverter.GetBytes( num ), 0, 4 );
        }
        public static uint ReadUInt24( this Stream s ) {
            int b1 = s.ReadByte();
            int b2 = s.ReadByte();
            int b3 = s.ReadByte();

            return (uint)( b3 << 16 | b2 << 8 | b1 );
        }
        public static uint PeekUInt24( this Stream s ) {
            long pos = s.Position;
            uint retval = s.ReadUInt24();
            s.Position = pos;
            return retval;
        }
        public static ushort ReadUInt16( this Stream s ) {
            int b1 = s.ReadByte();
            int b2 = s.ReadByte();

            return (ushort)( b2 << 8 | b1 );
        }
        public static ushort PeekUInt16( this Stream s ) {
            long pos = s.Position;
            ushort retval = s.ReadUInt16();
            s.Position = pos;
            return retval;
        }
        public static byte PeekByte( this Stream s ) {
            long pos = s.Position;
            int retval = s.ReadByte();
            s.Position = pos;
            return Convert.ToByte( retval );
        }
        public static void DiscardBytes( this Stream s, uint count ) {
            s.Position = s.Position + count;
        }
        public static void WriteUInt16( this Stream s, ushort num ) {
            s.Write( BitConverter.GetBytes( num ), 0, 2 );
        }

        public static void ReadAlign( this Stream s, long alignment ) {
            while ( s.Position % alignment != 0 ) {
                s.DiscardBytes( 1 );
            }
        }
        public static void WriteAlign( this Stream s, long alignment, byte paddingByte = 0 ) {
            while ( s.Position % alignment != 0 ) {
                s.WriteByte( paddingByte );
            }
        }

        public static void Write( this Stream s, byte[] data ) {
            s.Write( data, 0, data.Length );
        }
        #endregion
    }
}
