using System;
using System.Collections.Generic;
using System.IO;

namespace Foole.Mpq
{
    public sealed class BlockTable : MpqTable
    {
        internal const string TableKey = "(block table)";

        //private MpqEntry[] _entries;
        private uint _offset;
        //private uint _count;

        private List<MpqEntry> _entries;

        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public BlockTable( uint size, uint headerOffset ) : base( size )
        {
            //_entries = new MpqEntry[_size];

            _entries = new List<MpqEntry>( (int)size );

            _offset = headerOffset;
            //_count = 0;
        }

        public override string Key => TableKey;

        protected override int EntrySize => (int)MpqEntry.Size;

        /// <exception cref="NullReferenceException"></exception>
        public void Add( MpqEntry entry )
        {
            if ( !entry.IsAdded )
            {
                throw new InvalidOperationException( "Cannot add an MpqEntry to the BlockTable before its FilePos is known." );
            }

            //_entries[_count++] = entry;
            _entries.Add( entry );
        }

        /*public void Add( ref uint filePos, uint compressedSize, uint fileSize, MpqFileFlags flags )
        {
            Add( new MpqEntry( filePos, compressedSize, fileSize, flags ) );

            filePos += compressedSize;
        }*/

        public void UpdateSize()
        {
            _size = (uint)_entries.Count;
        }

        protected override void WriteEntry( BinaryWriter writer, int i )
        {
            var entry = _entries[i];

            // TODO: make method in MpqEntry for this?
            writer.Write( entry.FilePos + _offset );
            writer.Write( entry.CompressedSize );
            writer.Write( entry.FileSize );
            writer.Write( (uint)entry.Flags );
        }
    }
}