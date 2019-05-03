//
// MpqArchive.cs
//
// Authors:
//		Foole (fooleau@gmail.com)
//
// (C) 2006 Foole (fooleau@gmail.com)
// Based on code from StormLib by Ladislav Zezula
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Foole.Mpq
{
	public class MpqArchive : IDisposable, IEnumerable<MpqEntry>
	{
		private MpqHeader _mpqHeader;
		private long _headerOffset;
		private MpqHash[] _hashes;
		private MpqEntry[] _entries;
		
		private static uint[] sStormBuffer;

        internal Stream BaseStream { get; private set; }
        internal int BlockSize { get; private set; }
        internal uint HashEntryMask => _mpqHeader.HashTableSize - 1;

        static MpqArchive()
		{
			sStormBuffer = BuildStormBuffer();
		}

        /// <exception cref="FileNotFoundException"></exception>
        public MpqArchive(string filename)
		{
			BaseStream = File.Open(filename, FileMode.Open, FileAccess.Read);
			Init();
		}
		
		public MpqArchive(Stream sourceStream)
		{
            BaseStream = sourceStream;
			Init();
		}

        public MpqArchive(Stream sourceStream, bool loadListfile)
        {
            BaseStream = sourceStream;
            Init();
            if (loadListfile)
                AddListfileFilenames();
        }

        /// <exception cref="IOException"></exception>
        public MpqArchive( string filename, ICollection<MpqFile> mpqFiles )
        {
            _headerOffset = 0;

            BaseStream = File.Open( filename, FileMode.CreateNew, FileAccess.Write );
            Build( mpqFiles );
        }

        public MpqArchive( Stream sourceStream, ICollection<MpqFile> mpqFiles )
        {
            BaseStream = sourceStream;

            // The MPQ header will always start at an offset aligned to 512 bytes.
            var i = (uint)BaseStream.Position & ( 0x200 - 1 );
            if ( i > 0 )
            {
                Console.WriteLine( "Warning: Pre-Archive Data was not aligned to 512 bytes." );
                for ( ; i < 0x200; i++ )
                {
                    BaseStream.WriteByte( 0 );
                }
            }

            _headerOffset = BaseStream.Position;

            Build( mpqFiles );
        }

        /// <exception cref="IOException"></exception>
        public MpqArchive( string filename, BlockTable blockTable, HashTable hashTable, ICollection<MpqFile> mpqFiles )
        {
            _headerOffset = 0;

            BaseStream = File.Open( filename, FileMode.CreateNew, FileAccess.Write );
            Build( blockTable, hashTable, mpqFiles );
        }

        public MpqArchive( Stream sourceStream, BlockTable blockTable, HashTable hashTable, ICollection<MpqFile> mpqFiles, ushort blockSize )
        {
            BaseStream = sourceStream;

            // dont copypaste this part
            // The MPQ header will always start at an offset aligned to 512 bytes.
            var i = (uint)BaseStream.Position & ( 0x200 - 1 );
            if ( i > 0 )
            {
                Console.WriteLine( "Warning: Pre-Archive Data was not aligned to 512 bytes." );
                for ( ; i < 0x200; i++ )
                {
                    BaseStream.WriteByte( 0 );
                }
            }

            _headerOffset = BaseStream.Position;

            Build( blockTable, hashTable, mpqFiles, blockSize );
        }

        public void Dispose()
		{
            if (BaseStream != null)
                BaseStream.Close();
		}

		private void Init()
		{
			if (LocateMpqHeader() == false)
                throw new MpqParserException("Unable to find MPQ header");

            if (_mpqHeader.HashTableOffsetHigh != 0 || _mpqHeader.ExtendedBlockTableOffset != 0 || _mpqHeader.BlockTableOffsetHigh != 0)
                throw new MpqParserException("MPQ format version 1 features are not supported");

            BinaryReader br = new BinaryReader(BaseStream);

            BlockSize = 0x200 << _mpqHeader.BlockSize;

			// Load hash table
            BaseStream.Seek(_mpqHeader.HashTablePos, SeekOrigin.Begin);
			byte[] hashdata = br.ReadBytes((int)(_mpqHeader.HashTableSize * MpqHash.Size));
			MpqTable.Decrypt(hashdata, HashTable.TableKey);

			BinaryReader br2 = new BinaryReader(new MemoryStream(hashdata));
			_hashes = new MpqHash[_mpqHeader.HashTableSize];

			for (int i = 0; i < _mpqHeader.HashTableSize; i++)
				_hashes[i] = new MpqHash( br2, HashEntryMask );

			// Load entry table
            BaseStream.Seek(_mpqHeader.BlockTablePos, SeekOrigin.Begin);
			byte[] entrydata = br.ReadBytes((int)(_mpqHeader.BlockTableSize * MpqEntry.Size));
			MpqTable.Decrypt(entrydata, BlockTable.TableKey);

			br2 = new BinaryReader(new MemoryStream(entrydata));
			_entries = new MpqEntry[_mpqHeader.BlockTableSize];

			for (int i = 0; i < _mpqHeader.BlockTableSize; i++)
                _entries[i] = new MpqEntry(br2, (uint)_headerOffset);
		}
		
		private bool LocateMpqHeader()
		{
            BinaryReader br = new BinaryReader(BaseStream);

			// In .mpq files the header will be at the start of the file
			// In .exe files, it will be at a multiple of 0x200
            for (long i = 0; i < BaseStream.Length - MpqHeader.Size; i += 0x200)
			{
                BaseStream.Seek(i, SeekOrigin.Begin);
				_mpqHeader = MpqHeader.FromReader(br);
                if (_mpqHeader != null)
                {
					_headerOffset = i;
                    _mpqHeader.SetHeaderOffset(_headerOffset);
					return true;
				}
			}
			return false;
        }

        private void Build( ICollection<MpqFile> mpqFiles, ushort blockSize = 8 )
        {
            throw new NotImplementedException();
            // var blockTable = new BlockTable();
            // var hashTable = new HashTable();

            //Build( blockTable, hashTable, mpqFiles, blockSize );
        }

        private void Build( BlockTable blockTable, HashTable hashTable, ICollection<MpqFile> mpqFiles, ushort blockSize = 8 )
        {
            using ( var writer = new BinaryWriter( BaseStream, new UTF8Encoding( false, true ), true ) )
            {
                // Skip the MPQ header, since its contents will be calculated afterwards.
                writer.Seek( (int)MpqHeader.Size, SeekOrigin.Current );

                const bool archiveBeforeTables = true;
                uint hashTableEntries = 0;

                // Write Archive
                var fileIndex = (uint)0;
                var filePos = (uint)0;
                // TODO: add support for encryption of the archive files
                foreach ( var mpqFile in mpqFiles )
                {
                    uint locale = 0;
                    mpqFile.AddToArchive( fileIndex, filePos, locale, hashTable.Mask );

                    if ( archiveBeforeTables )
                    {
                        mpqFile.WriteToStream( writer );
                    }

                    hashTableEntries += hashTable.Add( mpqFile.MpqHash, mpqFile.HashIndex, mpqFile.HashCollisions );
                    blockTable.Add( mpqFile.MpqEntry );

                    filePos += mpqFile.MpqEntry.CompressedSize;
                    fileIndex++;
                }

                // Match size of blocktable with amount of occupied entries in hashtable
                /*
                for ( var i = blockTable.Size; i < hashTableEntries; i++ )
                {
                    var entry = MpqEntry.Dummy;
                    entry.SetPos( filePos );
                    blockTable.Add( entry );
                }
                blockTable.UpdateSize();
                */

                hashTable.WriteToStream( writer );
                blockTable.WriteToStream( writer );
                
                if ( !archiveBeforeTables )
                {
                    foreach ( var mpqFile in mpqFiles )
                    {
                        mpqFile.WriteToStream( writer );
                    }
                }

                writer.Seek( (int)_headerOffset, SeekOrigin.Begin );

                _mpqHeader = new MpqHeader( filePos, hashTable.Size, blockTable.Size, blockSize, archiveBeforeTables );
                _mpqHeader.WriteToStream( writer );
            }
        }

        public MpqStream OpenFile(string filename)
		{
			MpqHash hash;
			MpqEntry entry;

			if (!TryGetHashEntry(filename, out hash))
				throw new FileNotFoundException("File not found: " + filename);

            entry = _entries[hash.BlockIndex];
            if (entry.Filename == null)
                entry.Filename = filename;

            return new MpqStream(this, entry);
		}

        public MpqStream OpenFile(MpqEntry entry)
        {
            return new MpqStream(this, entry);
        }

		public bool FileExists(string filename)
		{
			MpqHash hash;
            
            return TryGetHashEntry(filename, out hash);
		}

        public bool AddListfileFilenames()
        {
            if (!AddFilename(ListFile.Key)) return false;

            using (Stream s = OpenFile(ListFile.Key))
                AddFilenames(s);

            return true;
        }

        public int AddFilenames(Stream stream, bool leaveOpen = false)
        {
            var filesFound = 0;
            using (StreamReader sr = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen))
            {
                while (!sr.EndOfStream)
                    if (AddFilename(sr.ReadLine()))
                        filesFound++;
            }
            return filesFound;
        }

        public bool AddFilename(string filename)
        {
            MpqHash hash;
            if (!TryGetHashEntry(filename, out hash)) return false;

            _entries[hash.BlockIndex].Filename = filename;
            return true;
        }

        public MpqEntry this[int index]
        {
            get { return _entries[index]; }
        }

        public MpqEntry this[string filename]
        {
            get 
            {
                MpqHash hash;
                if (!TryGetHashEntry(filename, out hash)) return null;
                return _entries[hash.BlockIndex];
            }
        }

        public int Count
        { 
            get { return _entries.Length; } 
        }

        public MpqHeader Header
        {
            get { return _mpqHeader; }
        }

		private bool TryGetHashEntry(string filename, out MpqHash hash)
		{
			uint index = HashString(filename, 0);
			index  &= _mpqHeader.HashTableSize - 1;
			uint name1 = HashString(filename, 0x100);
			uint name2 = HashString(filename, 0x200);

			for(uint i = index; i < _hashes.Length; ++i)
			{
				hash = _hashes[i];
                if (hash.Name1 == name1 && hash.Name2 == name2)
                    return true;
			}
            for (uint i = 0; i < index; i++)
            {
                hash = _hashes[i];
                if (hash.Name1 == name1 && hash.Name2 == name2)
                    return true;
            }

            hash = new MpqHash();
            return false;
        }

        private int TryGetHashEntry( int entryIndex, out MpqHash hash )
        {
            for ( var i = 0; i < _hashes.Length; i++ )
            {
                if ( _hashes[i].BlockIndex == entryIndex )
                {
                    hash = _hashes[i];
                    return i;
                }
            }

            hash = MpqHash.NULL;
            return -1;
        }

        private uint FindCollidingHashEntries( uint hashIndex, bool returnOnUnknown )
        {
            var count = (uint)0;
            var initial = hashIndex;
            for ( ; hashIndex >= 0; count++ )
            {
                if ( _hashes[--hashIndex].IsEmpty() )
                {
                    return count;
                }
                else if ( returnOnUnknown && _entries[_hashes[hashIndex].BlockIndex].Filename == null )
                {
                    return count;
                }
            }
            hashIndex = HashEntryMask;
            for ( ; hashIndex > initial; count++ )
            {
                if ( _hashes[--hashIndex].IsEmpty() )
                {
                    return count;
                }
                else if ( returnOnUnknown && _entries[_hashes[hashIndex].BlockIndex].Filename == null )
                {
                    return count;
                }
            }
            return count;
        }

        internal static uint HashString(string input, int offset)
		{
			uint seed1 = 0x7fed7fed;
			uint seed2 = 0xeeeeeeee;
			
			foreach(char c in input)
			{
				int val = (int)char.ToUpper(c);
				seed1 = sStormBuffer[offset + val] ^ (seed1 + seed2);
				seed2 = (uint)val + seed1 + seed2 + (seed2 << 5) + 3;
			}
			return seed1;
        }

        internal static void EncryptBlock( byte[] data, uint seed1 )
        {
            uint seed2 = 0xeeeeeeee;

            // NB: If the block is not an even multiple of 4,
            // the remainder is not encrypted
            for ( int i = 0; i < data.Length - 3; i += 4 )
            {
                seed2 += sStormBuffer[0x400 + ( seed1 & 0xff )];

                uint unencrypted = BitConverter.ToUInt32(data, i);
                uint result = unencrypted ^ ( seed1 + seed2 );

                seed1 = ( ( ~seed1 << 21 ) + 0x11111111 ) | ( seed1 >> 11 );
                seed2 = unencrypted + seed2 + ( seed2 << 5 ) + 3;

                data[i + 0] = ( (byte)( result & 0xff ) );
                data[i + 1] = ( (byte)( ( result >> 8 ) & 0xff ) );
                data[i + 2] = ( (byte)( ( result >> 16 ) & 0xff ) );
                data[i + 3] = ( (byte)( ( result >> 24 ) & 0xff ) );
            }
        }

        internal static void EncryptBlock( uint[] data, uint seed1 )
        {
            uint seed2 = 0xeeeeeeee;

            for ( int i = 0; i < data.Length; i++ )
            {
                seed2 += sStormBuffer[0x400 + ( seed1 & 0xff )];

                uint unencrypted = data[i];
                uint result = unencrypted ^ (seed1 + seed2);

                seed1 = ( ( ~seed1 << 21 ) + 0x11111111 ) | ( seed1 >> 11 );
                seed2 = unencrypted + seed2 + ( seed2 << 5 ) + 3;

                data[i] = result;
            }
        }

		internal static void DecryptBlock(byte[] data, uint seed1)
		{
			uint seed2 = 0xeeeeeeee;

			// NB: If the block is not an even multiple of 4,
			// the remainder is not encrypted
			for (int i = 0; i < data.Length - 3; i += 4)
			{
				seed2 += sStormBuffer[0x400 + (seed1 & 0xff)];

				uint result = BitConverter.ToUInt32(data, i);
				result ^= (seed1 + seed2);

				seed1 = ((~seed1 << 21) + 0x11111111) | (seed1 >> 11);
				seed2 = result + seed2 + (seed2 << 5) + 3;

				data[i + 0] = ((byte)(result & 0xff));
				data[i + 1] = ((byte)((result >> 8) & 0xff));
				data[i + 2] = ((byte)((result >> 16) & 0xff));
				data[i + 3] = ((byte)((result >> 24) & 0xff));
			}
		}
		
		internal static void DecryptBlock(uint[] data, uint seed1)
		{
			uint seed2 = 0xeeeeeeee;

			for (int i = 0; i < data.Length; i++)
			{
				seed2 += sStormBuffer[0x400 + (seed1 & 0xff)];
				uint result = data[i];
				result ^= seed1 + seed2;

				seed1 = ((~seed1 << 21) + 0x11111111) | (seed1 >> 11);
				seed2 = result + seed2 + (seed2 << 5) + 3;
				data[i] = result;
			}
		}

        // This function calculates the encryption key based on
		// some assumptions we can make about the headers for encrypted files
		internal static uint DetectFileSeed(uint value0, uint value1, uint decrypted)
		{
            uint temp = (value0 ^ decrypted) - 0xeeeeeeee;

            for (int i = 0; i < 0x100; i++)
            {
                uint seed1 = temp - sStormBuffer[0x400 + i];
                uint seed2 = 0xeeeeeeee + sStormBuffer[0x400 + (seed1 & 0xff)];
                uint result = value0 ^ (seed1 + seed2);

                if (result != decrypted)
                    continue;

                uint saveseed1 = seed1;

                // Test this result against the 2nd value
                seed1 = ((~seed1 << 21) + 0x11111111) | (seed1 >> 11);
                seed2 = result + seed2 + (seed2 << 5) + 3;

                seed2 += sStormBuffer[0x400 + (seed1 & 0xff)];
                result = value1 ^ (seed1 + seed2);

                if ((result & 0xfffc0000) == 0)
                    return saveseed1;
            }
            return 0;
        }

        private static uint[] BuildStormBuffer()
		{
			uint seed = 0x100001;
			
			uint[] result = new uint[0x500];
			
			for(uint index1 = 0; index1 < 0x100; index1++)
			{
				uint index2 = index1;
				for(int i = 0; i < 5; i++, index2 += 0x100)
				{
					seed = (seed * 125 + 3) % 0x2aaaab;
					uint temp = (seed & 0xffff) << 16;
					seed = (seed * 125 + 3) % 0x2aaaab;

					result[index2]  = temp | (seed & 0xffff);
				}
			}

			return result;
		}

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _entries.GetEnumerator();
        }

        IEnumerator<MpqEntry> IEnumerable<MpqEntry>.GetEnumerator()
        {
            foreach (MpqEntry entry in _entries)
                yield return entry;
        }
    }
}