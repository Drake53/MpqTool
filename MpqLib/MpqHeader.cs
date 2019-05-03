﻿using System;
using System.IO;

namespace Foole.Mpq
{
    // TODO: Possibly incorporate this into MpqArchive
    public class MpqHeader
    {
        public uint ID { get; private set; } // Signature.  Should be 0x1a51504d
        public uint DataOffset { get; private set; } // Offset of the first file.  AKA Header size
        public uint ArchiveSize { get; private set; }
        public ushort MpqVersion { get; private set; } // Most are 0.  Burning Crusade = 1
        public ushort BlockSize { get; private set; } // Size of file block is 0x200 << BlockSize
        public uint HashTablePos { get; private set; }
        public uint BlockTablePos { get; private set; }
        public uint HashTableSize { get; private set; }
        public uint BlockTableSize { get; private set; }

        // Version 1 fields
        // The extended block table is an array of Int16 - higher bits of the offests in the block table.
        public Int64 ExtendedBlockTableOffset { get; private set; }
        public short HashTableOffsetHigh { get; private set; }
        public short BlockTableOffsetHigh { get; private set; }


        public static readonly uint MpqId = 0x1a51504d;
        public static readonly uint Size = 32;

        public static MpqHeader FromReader( BinaryReader br )
        {
            uint id = br.ReadUInt32();
            if ( id != MpqId ) return null;
            MpqHeader header = new MpqHeader
            {
                ID = id,
                DataOffset = br.ReadUInt32(),
                ArchiveSize = br.ReadUInt32(),
                MpqVersion = br.ReadUInt16(),
                BlockSize = br.ReadUInt16(),
                HashTablePos = br.ReadUInt32(),
                BlockTablePos = br.ReadUInt32(),
                HashTableSize = br.ReadUInt32(),
                BlockTableSize = br.ReadUInt32(),
            };

#if DEBUG
            if ( header.MpqVersion == 0 )
            {
                // Check validity
                if ( Size != header.DataOffset )
                    throw new MpqParserException( string.Format( "Invalid MPQ header field: DataOffset. Expected {0}, was {1}", Size, header.DataOffset ) );

                if ( header.ArchiveSize != header.BlockTablePos + MpqEntry.Size * header.BlockTableSize )
                    throw new MpqParserException( string.Format( "Invalid MPQ header field: ArchiveSize. Was {0}, expected {1}", header.ArchiveSize, header.BlockTablePos + MpqEntry.Size * header.BlockTableSize ) );
                if ( header.HashTablePos != header.ArchiveSize - MpqHash.Size * header.HashTableSize - MpqEntry.Size * header.BlockTableSize )
                    throw new MpqParserException( string.Format( "Invalid MPQ header field: HashTablePos. Was {0}, expected {1}", header.HashTablePos, header.ArchiveSize - MpqHash.Size * header.HashTableSize - MpqEntry.Size * header.BlockTableSize ) );
                if ( header.BlockTablePos != header.HashTablePos + MpqHash.Size * header.HashTableSize )
                    throw new MpqParserException( string.Format( "Invalid MPQ header field: BlockTablePos. Was {0}, expected {1}", header.BlockTablePos, header.HashTablePos + MpqHash.Size * header.HashTableSize ) );
            }
#endif

            if ( header.MpqVersion == 1 )
            {
                header.ExtendedBlockTableOffset = br.ReadInt64();
                header.HashTableOffsetHigh = br.ReadInt16();
                header.BlockTableOffsetHigh = br.ReadInt16();
            }

            return header;
        }

        public MpqHeader()
        {
            ID = MpqId;
        }

        public MpqHeader( uint fileArchiveSize, uint hashTableEntries, uint blockTableEntries, ushort blockSize, bool archiveBeforeTables = true )
            : this()
        {
            var hashTableSize = hashTableEntries * MpqHash.Size;
            var blockTableSize = blockTableEntries * MpqEntry.Size;

            if ( archiveBeforeTables )
            {
                // MPQ contents are in order: header, archive, HT, BT
                DataOffset = Size;
                ArchiveSize = Size + fileArchiveSize + hashTableSize + blockTableSize;
                MpqVersion = 0;
                BlockSize = blockSize;
                HashTablePos = Size + fileArchiveSize;
                BlockTablePos = Size + fileArchiveSize + hashTableSize;
                HashTableSize = hashTableEntries;
                BlockTableSize = blockTableEntries;
            }
            else
            {
                // MPQ contents are in order: header, HT, BT, archive
                throw new NotImplementedException();
            }
        }

        public void WriteToStream( BinaryWriter writer )
        {
            writer.Write( MpqId );
            writer.Write( DataOffset );
            writer.Write( ArchiveSize );
            writer.Write( MpqVersion );
            writer.Write( BlockSize );
            writer.Write( HashTablePos );
            writer.Write( BlockTablePos );
            writer.Write( HashTableSize );
            writer.Write( BlockTableSize );
        }

        public void SetHeaderOffset( long headerOffset )
        {
            HashTablePos += (uint)headerOffset;
            BlockTablePos += (uint)headerOffset;
            if ( DataOffset == 0x6d9e4b86 ) // A protected archive.  Seen in some custom wc3 maps.
                DataOffset = (uint)( MpqHeader.Size + headerOffset );
        }
    }
}