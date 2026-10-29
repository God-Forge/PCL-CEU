#include "payload_common.hpp"

#include <algorithm>
#include <array>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>

using namespace payload;

namespace {

	int run( const std::vector<std::wstring>& args ) {
		if ( args.size() < 3 ) {
			print_error( "Usage: encode.exe <input-folder> <output.payload> [--name <version-name>]" );
			return 1;
		}

		fs::path input = args[1];
		fs::path output = args[2];
		if ( !fs::is_directory( input ) ) {
			print_error( "Input folder does not exist: " + wide_to_utf8( input.wstring() ) );
			return 1;
		}

		std::string name;
		if ( args.size() >= 5 && args[3] == L"--name" ) {
			name = wide_to_utf8( args[4] );
		} else {
			name = wide_to_utf8( input.filename().wstring() );
		}
		if ( name.empty() ) name = "minecraft-version";

		std::vector<fs::path> files;
		std::error_code outputEc;
		const fs::path outputCanonical = fs::weakly_canonical( output, outputEc );
		for ( const auto& entry : fs::recursive_directory_iterator( input, fs::directory_options::skip_permission_denied ) ) {
			std::error_code ec;
			if ( entry.is_symlink( ec ) ) continue;
			if ( !entry.is_regular_file( ec ) ) continue;
			std::error_code fileEc;
			if ( !outputEc && fs::weakly_canonical( entry.path(), fileEc ) == outputCanonical ) continue;
			files.push_back( entry.path() );
		}
		std::sort( files.begin(), files.end(), [&input]( const fs::path & left, const fs::path & right ) {
			std::error_code ec1, ec2;
			auto leftName = fs::relative( left, input, ec1 ).generic_u8string();
			auto rightName = fs::relative( right, input, ec2 ).generic_u8string();
			return leftName < rightName;
		} );

		if ( files.empty() ) {
			print_error( "Input folder contains no files." );
			return 1;
		}

		if ( !output.parent_path().empty() ) {
			std::error_code ec;
			fs::create_directories( output.parent_path(), ec );
			if ( ec ) throw std::runtime_error( "Cannot create output folder: " + ec.message() );
		}

		const std::time_t created = std::time( nullptr );
		const std::string metadata =
		    "{\"format\":\"pcl-payload\",\"version\":1,\"name\":\"" + json_escape( name ) +
		    "\",\"type\":\"minecraft-version\",\"created\":" + std::to_string( created ) +
		    ",\"entries\":" + std::to_string( files.size() ) + "}";

		std::ofstream out( output, std::ios::binary | std::ios::trunc );
		if ( !out ) throw std::runtime_error( "Cannot create output file." );

		std::array<char, kHeaderSize> placeholder{};
		out.write( placeholder.data(), placeholder.size() );
		out.write( metadata.data(), static_cast<std::streamsize>( metadata.size() ) );
		if ( !out ) throw std::runtime_error( "Failed to write payload header." );

		ZipWriter writer( out );
		for ( const auto& file : files ) {
			std::error_code ec;
			fs::path relative = fs::relative( file, input, ec );
			if ( ec ) throw std::runtime_error( "Cannot resolve relative path: " + ec.message() );
			std::string zipName = relative.generic_u8string();
			if ( zipName.empty() ) throw std::runtime_error( "Empty ZIP entry name." );
			writer.add_file( file, zipName );
		}
		writer.finish();

		Header header;
		header.magic = kMagic;
		header.version = kVersion;
		header.header_size = kHeaderSize;
		header.flags = kFlagStored | kFlagUtf8;
		header.zip_offset = kHeaderSize + metadata.size();
		header.zip_size = writer.zip_size();
		header.metadata_size = static_cast<uint32_t>( metadata.size() );
		header.metadata_crc32 = crc32( metadata.data(), metadata.size() );
		header.zip_crc32 = writer.zip_crc32();
		header.entry_count = writer.entry_count();
		header.created_unix = static_cast<uint64_t>( created );

		out.seekp( 0, std::ios::beg );
		write_header( out, header );
		out.close();
		if ( !out ) throw std::runtime_error( "Failed to finalize payload." );

		const uint64_t totalSize = header.zip_offset + header.zip_size;
		print_line( "Encoded " + std::to_string( header.entry_count ) + " files -> " +
		            wide_to_utf8( output.wstring() ) + " (" + std::to_string( totalSize ) + " bytes)" );
		return 0;
	}

} // namespace

int main() {
	enable_utf8_console();
	try {
		return run( command_line_args() );
	} catch ( const std::exception& ex ) {
		print_error( std::string( "encode failed: " ) + ex.what() );
		return 2;
	}
}
