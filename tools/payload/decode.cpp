#include "payload_common.hpp"

#include <array>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>

using namespace payload;

namespace {
	uint16_t read_u16( std::istream& in ) {
		std::array<uint8_t, 2> buffer{};
		in.read( reinterpret_cast<char*>( buffer.data() ), buffer.size() );
		if ( in.gcount() != 2 ) throw std::runtime_error( "Unexpected end of ZIP header." );
		return le16( buffer.data() );
	}
	uint32_t read_u32( std::istream& in ) {
		std::array<uint8_t, 4> buffer{};
		in.read( reinterpret_cast<char*>( buffer.data() ), buffer.size() );
		if ( in.gcount() != 4 ) throw std::runtime_error( "Unexpected end of ZIP header." );
		return le32( buffer.data() );
	}

	uint32_t crc32_stream( std::ifstream& in, uint64_t offset, uint64_t length ) {
		in.clear();
		in.seekg( static_cast<std::streamoff>( offset ), std::ios::beg );
		if ( !in ) throw std::runtime_error( "Cannot seek to ZIP payload." );
		uint32_t state = crc32_begin();
		std::array < char, 1 << 20 > buffer{};
		uint64_t remaining = length;
		while ( remaining > 0 ) {
			const auto chunk = static_cast<std::streamsize>( std::min<uint64_t>( remaining, buffer.size() ) );
			in.read( buffer.data(), chunk );
			if ( in.gcount() != chunk ) throw std::runtime_error( "Unexpected end of ZIP payload." );
			state = crc32_update( state, buffer.data(), static_cast<size_t>( chunk ) );
			remaining -= static_cast<uint64_t>( chunk );
		}
		return crc32_end( state );
	}

	int run( const std::vector<std::wstring>& args ) {
		if ( args.size() < 2 ) {
			print_error( "Usage: decode.exe <input.payload> <output-folder>" );
			print_error( "       decode.exe --info <input.payload>" );
			return 1;
		}

		const bool infoOnly = args[1] == L"--info";
		const size_t payloadIndex = infoOnly ? 2 : 1;
		if ( args.size() <= payloadIndex ) {
			print_error( "Missing payload path." );
			return 1;
		}
		if ( !infoOnly && args.size() < 3 ) {
			print_error( "Missing output folder." );
			return 1;
		}

		const fs::path payloadPath = args[payloadIndex];
		if ( !fs::is_regular_file( payloadPath ) ) {
			print_error( "Payload file does not exist: " + wide_to_utf8( payloadPath.wstring() ) );
			return 1;
		}

		std::ifstream in( payloadPath, std::ios::binary );
		if ( !in ) throw std::runtime_error( "Cannot open payload." );

		Header header = read_header( in );
		if ( header.magic != kMagic ) throw std::runtime_error( "Invalid payload magic." );
		if ( header.version != kVersion ) throw std::runtime_error( "Unsupported payload version." );
		if ( header.header_size != kHeaderSize ) throw std::runtime_error( "Unsupported payload header size." );
		if ( ( header.flags & kFlagStored ) == 0 ) throw std::runtime_error( "Only stored ZIP payloads are supported." );

		const uint64_t fileSize = fs::file_size( payloadPath );
		if ( header.metadata_size > ( 1u << 20 ) ) throw std::runtime_error( "Payload metadata is too large." );
		if ( header.zip_offset != kHeaderSize + header.metadata_size ) throw std::runtime_error( "Invalid payload ZIP offset." );
		if ( header.zip_size == 0 || header.zip_offset > fileSize || header.zip_size > fileSize - header.zip_offset ) {
			throw std::runtime_error( "Payload is truncated." );
		}

		std::string metadata( header.metadata_size, '\0' );
		if ( !metadata.empty() ) {
			in.read( metadata.data(), static_cast<std::streamsize>( metadata.size() ) );
			if ( in.gcount() != static_cast<std::streamsize>( metadata.size() ) ) throw std::runtime_error( "Payload metadata is truncated." );
		}
		if ( crc32( metadata.data(), metadata.size() ) != header.metadata_crc32 ) {
			throw std::runtime_error( "Payload metadata checksum mismatch." );
		}

		if ( infoOnly ) {
			print_line( "Payload: " + wide_to_utf8( payloadPath.wstring() ) );
			print_line( "Format version: " + std::to_string( header.version ) );
			print_line( "ZIP offset: " + std::to_string( header.zip_offset ) );
			print_line( "ZIP size: " + std::to_string( header.zip_size ) );
			print_line( "Entries: " + std::to_string( header.entry_count ) );
			print_line( "Created (Unix): " + std::to_string( header.created_unix ) );
			print_line( "Metadata: " + metadata );
			return 0;
		}

		const fs::path output = args[2];
		std::error_code ec;
		fs::create_directories( output, ec );
		if ( ec ) throw std::runtime_error( "Cannot create output folder: " + ec.message() );

		if ( crc32_stream( in, header.zip_offset, header.zip_size ) != header.zip_crc32 ) {
			throw std::runtime_error( "Payload ZIP checksum mismatch." );
		}

		in.clear();
		in.seekg( static_cast<std::streamoff>( header.zip_offset ), std::ios::beg );
		if ( !in ) throw std::runtime_error( "Cannot seek to ZIP payload." );

		uint32_t extracted = 0;
		std::array < char, 1 << 20 > buffer{};
		while ( true ) {
			const std::streamoff position = in.tellg();
			if ( position < 0 || static_cast<uint64_t>( position ) >= header.zip_offset + header.zip_size ) break;

			const uint32_t signature = read_u32( in );
			if ( signature == 0x02014b50 || signature == 0x06054b50 ) break;
			if ( signature != 0x04034b50 ) throw std::runtime_error( "Invalid ZIP local header signature." );

			( void )read_u16( in ); // version needed
			const uint16_t flags = read_u16( in );
			const uint16_t method = read_u16( in );
			( void )read_u16( in ); // time
			( void )read_u16( in ); // date
			const uint32_t expectedCrc = read_u32( in );
			const uint32_t compressedSize = read_u32( in );
			const uint32_t uncompressedSize = read_u32( in );
			const uint16_t nameLength = read_u16( in );
			const uint16_t extraLength = read_u16( in );

			if ( ( flags & 1 ) != 0 ) throw std::runtime_error( "Encrypted ZIP entries are not supported." );
			if ( method != 0 ) throw std::runtime_error( "Compressed ZIP entries are not supported." );
			if ( nameLength == 0 || nameLength > 32768 ) throw std::runtime_error( "Invalid ZIP entry name length." );
			if ( compressedSize != uncompressedSize ) throw std::runtime_error( "Stored ZIP entry size mismatch." );

			std::string name( nameLength, '\0' );
			in.read( name.data(), nameLength );
			if ( in.gcount() != nameLength ) throw std::runtime_error( "ZIP entry name is truncated." );
			if ( extraLength > 0 ) in.seekg( extraLength, std::ios::cur );

			const bool isDirectory = !name.empty() && name.back() == '/';
			std::string normalized;
			if ( !safe_zip_name( name, normalized ) ) throw std::runtime_error( "Unsafe ZIP entry path: " + name );

			const fs::path target = output / fs::u8path( normalized );
			if ( isDirectory ) {
				fs::create_directories( target, ec );
				if ( ec ) throw std::runtime_error( "Cannot create folder: " + ec.message() );
			} else {
				if ( !target.parent_path().empty() ) {
					fs::create_directories( target.parent_path(), ec );
					if ( ec ) throw std::runtime_error( "Cannot create parent folder: " + ec.message() );
				}

				std::ofstream out( target, std::ios::binary | std::ios::trunc );
				if ( !out ) throw std::runtime_error( "Cannot create output file: " + wide_to_utf8( target.wstring() ) );

				uint32_t state = crc32_begin();
				uint64_t remaining = compressedSize;
				while ( remaining > 0 ) {
					const auto chunk = static_cast<std::streamsize>( std::min<uint64_t>( remaining, buffer.size() ) );
					in.read( buffer.data(), chunk );
					if ( in.gcount() != chunk ) throw std::runtime_error( "ZIP entry data is truncated." );
					out.write( buffer.data(), chunk );
					if ( !out ) throw std::runtime_error( "Failed to write output file." );
					state = crc32_update( state, buffer.data(), static_cast<size_t>( chunk ) );
					remaining -= static_cast<uint64_t>( chunk );
				}
				if ( crc32_end( state ) != expectedCrc ) throw std::runtime_error( "ZIP entry checksum mismatch: " + normalized );
			}

			++extracted;
		}

		if ( extracted != header.entry_count ) {
			print_error( "Warning: extracted entry count does not match header (" +
			             std::to_string( extracted ) + " != " + std::to_string( header.entry_count ) + ")." );
		}

		print_line( "Decoded " + std::to_string( extracted ) + " entries -> " + wide_to_utf8( output.wstring() ) );
		return 0;
	}

} // namespace

int main() {
	enable_utf8_console();
	try {
		return run( command_line_args() );
	} catch ( const std::exception& ex ) {
		print_error( std::string( "decode failed: " ) + ex.what() );
		return 2;
	}
}
