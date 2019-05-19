﻿using System;

internal class StormBuffer
{
    private static uint[] _stormBuffer;

    static StormBuffer()
    {
        uint seed = 0x100001;

        _stormBuffer = new uint[0x500];

        for ( uint index1 = 0; index1 < 0x100; index1++ )
        {
            uint index2 = index1;
            for ( int i = 0; i < 5; i++, index2 += 0x100 )
            {
                seed = ( seed * 125 + 3 ) % 0x2aaaab;
                uint temp = ( seed & 0xffff ) << 16;
                seed = ( seed * 125 + 3 ) % 0x2aaaab;

                _stormBuffer[index2] = temp | ( seed & 0xffff );
            }
        }
    }

    internal static uint HashString( string input, int offset )
    {
        uint seed1 = 0x7fed7fed;
        uint seed2 = 0xeeeeeeee;

        foreach ( char c in input )
        {
            int val = (int)char.ToUpper( c );
            seed1 = _stormBuffer[offset + val] ^ ( seed1 + seed2 );
            seed2 = (uint)val + seed1 + seed2 + ( seed2 << 5 ) + 3;
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
            seed2 += _stormBuffer[0x400 + ( seed1 & 0xff )];

            uint unencrypted = BitConverter.ToUInt32( data, i );
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
            seed2 += _stormBuffer[0x400 + ( seed1 & 0xff )];

            uint unencrypted = data[i];
            uint result = unencrypted ^ ( seed1 + seed2 );

            seed1 = ( ( ~seed1 << 21 ) + 0x11111111 ) | ( seed1 >> 11 );
            seed2 = unencrypted + seed2 + ( seed2 << 5 ) + 3;

            data[i] = result;
        }
    }

    internal static void DecryptBlock( byte[] data, uint seed1 )
    {
        uint seed2 = 0xeeeeeeee;

        // NB: If the block is not an even multiple of 4,
        // the remainder is not encrypted
        for ( int i = 0; i < data.Length - 3; i += 4 )
        {
            seed2 += _stormBuffer[0x400 + ( seed1 & 0xff )];

            uint result = BitConverter.ToUInt32( data, i );
            result ^= ( seed1 + seed2 );

            seed1 = ( ( ~seed1 << 21 ) + 0x11111111 ) | ( seed1 >> 11 );
            seed2 = result + seed2 + ( seed2 << 5 ) + 3;

            data[i + 0] = ( (byte)( result & 0xff ) );
            data[i + 1] = ( (byte)( ( result >> 8 ) & 0xff ) );
            data[i + 2] = ( (byte)( ( result >> 16 ) & 0xff ) );
            data[i + 3] = ( (byte)( ( result >> 24 ) & 0xff ) );
        }
    }

    internal static void DecryptBlock( uint[] data, uint seed1 )
    {
        uint seed2 = 0xeeeeeeee;

        for ( int i = 0; i < data.Length; i++ )
        {
            seed2 += _stormBuffer[0x400 + ( seed1 & 0xff )];
            uint result = data[i];
            result ^= seed1 + seed2;

            seed1 = ( ( ~seed1 << 21 ) + 0x11111111 ) | ( seed1 >> 11 );
            seed2 = result + seed2 + ( seed2 << 5 ) + 3;
            data[i] = result;
        }
    }

    // This function calculates the encryption key based on
    // some assumptions we can make about the headers for encrypted files
    internal static uint DetectFileSeed( uint value0, uint value1, uint decrypted )
    {
        uint temp = ( value0 ^ decrypted ) - 0xeeeeeeee;

        for ( int i = 0; i < 0x100; i++ )
        {
            uint seed1 = temp - _stormBuffer[0x400 + i];
            uint seed2 = 0xeeeeeeee + _stormBuffer[0x400 + ( seed1 & 0xff )];
            uint result = value0 ^ ( seed1 + seed2 );

            if ( result != decrypted )
                continue;

            uint saveseed1 = seed1;

            // Test this result against the 2nd value
            seed1 = ( ( ~seed1 << 21 ) + 0x11111111 ) | ( seed1 >> 11 );
            seed2 = result + seed2 + ( seed2 << 5 ) + 3;

            seed2 += _stormBuffer[0x400 + ( seed1 & 0xff )];
            result = value1 ^ ( seed1 + seed2 );

            if ( ( result & 0xfffc0000 ) == 0 )
                return saveseed1;
        }
        return 0;
    }
}