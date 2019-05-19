﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Foole.Mpq
{
    /// <summary>
    /// The <see cref="BlockTable"/> of an <see cref="MpqArchive"/> contains the list of <see cref="MpqEntry"/> objects.
    /// </summary>
    public sealed class BlockTable : MpqTable, IEnumerable<MpqEntry>
    {
        internal const string TableKey = "(block table)";

        private List<MpqEntry> _entries;
        private uint _offset;

        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public BlockTable( uint size, uint headerOffset ) : base( size )
        {
            _entries = new List<MpqEntry>( (int)size );
            _offset = headerOffset;
        }

        internal BlockTable( BinaryReader reader, uint size, uint headerOffset ) : base( size )
        {
            _entries = new List<MpqEntry>( (int)size );
            _offset = headerOffset;

            var entrydata = reader.ReadBytes( (int)( size * MpqEntry.Size ) );
            Decrypt( entrydata, TableKey );

            using ( var stream = new MemoryStream( entrydata ) )
            {
                using ( var streamReader = new BinaryReader( stream ) )
                {
                    for ( var i = 0; i < size; i++ )
                    {
                        _entries[i] = new MpqEntry( streamReader, _offset );
                    }
                }
            }
        }

        /// <summary>
        /// The key used to encrypt and decrypt the <see cref="BlockTable"/>.
        /// </summary>
        public override string Key => TableKey;

        protected internal override int EntrySize => (int)MpqEntry.Size;

        public MpqEntry this[int index]
        {
            get => _entries[index];
        }

        public MpqEntry this[uint index]
        {
            get => _entries[(int)index];
        }

        /// <exception cref="NullReferenceException"></exception>
        public void Add( MpqEntry entry )
        {
            if ( !entry.IsAdded )
            {
                throw new InvalidOperationException( "Cannot add an MpqEntry to the BlockTable before its FilePos is known." );
            }

            _entries.Add( entry );
        }

        protected override void WriteEntry( BinaryWriter writer, int i )
        {
            var entry = _entries[i];

            writer.Write( entry.FilePos + _offset );
            writer.Write( entry.CompressedSize );
            writer.Write( entry.FileSize );
            writer.Write( (uint)entry.Flags );
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _entries.GetEnumerator();
        }

        IEnumerator<MpqEntry> IEnumerable<MpqEntry>.GetEnumerator()
        {
            foreach ( var entry in _entries )
            {
                yield return entry;
            }
        }
    }
}