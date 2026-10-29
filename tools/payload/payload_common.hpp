#pragma once

#include <windows.h>
#include <shellapi.h>

#include <algorithm>
#include <array>
#include <cctype>
#include <cstdint>
#include <cstring>
#include <ctime>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

namespace payload {

namespace fs = std::filesystem;

constexpr std::array<uint8_t, 8> kMagic = {'P', 'C', 'L', 'P', 'A', 'Y', '0', '1'};
constexpr uint16_t kVersion = 1;
constexpr uint16_t kHeaderSize = 128;
constexpr uint32_t kFlagStored = 1;
constexpr uint32_t kFlagUtf8 = 2;

#pragma pack(push, 1)
struct Header {
    std::array<uint8_t, 8> magic{};
    uint16_t version = kVersion;
    uint16_t header_size = kHeaderSize;
    uint32_t flags = kFlagStored | kFlagUtf8;
    uint64_t zip_offset = 0;
    uint64_t zip_size = 0;
    uint32_t metadata_size = 0;
    uint32_t metadata_crc32 = 0;
    uint32_t zip_crc32 = 0;
    uint32_t entry_count = 0;
    uint64_t created_unix = 0;
    uint8_t reserved[72]{};
};
#pragma pack(pop)

static_assert(sizeof(Header) == 128, "Header layout is not 128 bytes");

inline uint16_t le16(const uint8_t* p) {
    return static_cast<uint16_t>(p[0] | (p[1] << 8));
}
inline uint32_t le32(const uint8_t* p) {
    return static_cast<uint32_t>(p[0] | (p[1] << 8) | (p[2] << 16) | (p[3] << 24));
}
inline uint64_t le64(const uint8_t* p) {
    return static_cast<uint64_t>(le32(p)) | (static_cast<uint64_t>(le32(p + 4)) << 32);
}

inline void put_le16(uint8_t* p, uint16_t v) {
    p[0] = static_cast<uint8_t>(v);
    p[1] = static_cast<uint8_t>(v >> 8);
}
inline void put_le32(uint8_t* p, uint32_t v) {
    p[0] = static_cast<uint8_t>(v);
    p[1] = static_cast<uint8_t>(v >> 8);
    p[2] = static_cast<uint8_t>(v >> 16);
    p[3] = static_cast<uint8_t>(v >> 24);
}
inline void put_le64(uint8_t* p, uint64_t v) {
    put_le32(p, static_cast<uint32_t>(v));
    put_le32(p + 4, static_cast<uint32_t>(v >> 32));
}

inline uint32_t crc32_update(uint32_t state, const void* data, size_t length) {
    static uint32_t table[256]{};
    static bool initialized = false;
    if (!initialized) {
        for (uint32_t i = 0; i < 256; ++i) {
            uint32_t value = i;
            for (int bit = 0; bit < 8; ++bit) value = (value >> 1) ^ ((value & 1) ? 0xEDB88320u : 0);
            table[i] = value;
        }
        initialized = true;
    }
    auto* bytes = static_cast<const uint8_t*>(data);
    for (size_t i = 0; i < length; ++i) state = table[(state ^ bytes[i]) & 0xFF] ^ (state >> 8);
    return state;
}
inline uint32_t crc32_begin() { return 0xFFFFFFFFu; }
inline uint32_t crc32_end(uint32_t state) { return state ^ 0xFFFFFFFFu; }
inline uint32_t crc32(const void* data, size_t length) {
    return crc32_end(crc32_update(crc32_begin(), data, length));
}

inline std::wstring utf8_to_wide(const std::string& text) {
    if (text.empty()) return {};
    int size = MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0);
    if (size <= 0) throw std::runtime_error("UTF-8 conversion failed");
    std::wstring result(size, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), result.data(), size);
    return result;
}

inline std::string wide_to_utf8(const std::wstring& text) {
    if (text.empty()) return {};
    int size = WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
    if (size <= 0) throw std::runtime_error("UTF-8 conversion failed");
    std::string result(size, '\0');
    WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), result.data(), size, nullptr, nullptr);
    return result;
}

inline std::vector<std::wstring> command_line_args() {
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argv == nullptr) throw std::runtime_error("CommandLineToArgvW failed");
    std::vector<std::wstring> result;
    result.reserve(static_cast<size_t>(argc));
    for (int i = 0; i < argc; ++i) result.emplace_back(argv[i]);
    LocalFree(argv);
    return result;
}

inline void enable_utf8_console() {
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
}

inline void print_line(const std::string& text) {
    std::cout << text << '\n';
}
inline void print_error(const std::string& text) {
    std::cerr << text << '\n';
}

inline std::string json_escape(const std::string& text) {
    std::string result;
    result.reserve(text.size() + 8);
    for (unsigned char c : text) {
        switch (c) {
            case '"': result += "\\\""; break;
            case '\\': result += "\\\\"; break;
            case '\b': result += "\\b"; break;
            case '\f': result += "\\f"; break;
            case '\n': result += "\\n"; break;
            case '\r': result += "\\r"; break;
            case '\t': result += "\\t"; break;
            default:
                if (c < 0x20) {
                    char buffer[8];
                    std::snprintf(buffer, sizeof(buffer), "\\u%04x", c);
                    result += buffer;
                } else {
                    result += static_cast<char>(c);
                }
        }
    }
    return result;
}

inline void write_header(std::ostream& out, const Header& header) {
    std::array<uint8_t, kHeaderSize> buffer{};
    std::memcpy(buffer.data(), header.magic.data(), header.magic.size());
    put_le16(buffer.data() + 8, header.version);
    put_le16(buffer.data() + 10, header.header_size);
    put_le32(buffer.data() + 12, header.flags);
    put_le64(buffer.data() + 16, header.zip_offset);
    put_le64(buffer.data() + 24, header.zip_size);
    put_le32(buffer.data() + 32, header.metadata_size);
    put_le32(buffer.data() + 36, header.metadata_crc32);
    put_le32(buffer.data() + 40, header.zip_crc32);
    put_le32(buffer.data() + 44, header.entry_count);
    put_le64(buffer.data() + 48, header.created_unix);
    out.write(reinterpret_cast<const char*>(buffer.data()), buffer.size());
}

inline Header read_header(std::istream& in) {
    std::array<uint8_t, kHeaderSize> buffer{};
    in.read(reinterpret_cast<char*>(buffer.data()), buffer.size());
    if (in.gcount() != static_cast<std::streamsize>(buffer.size())) throw std::runtime_error("Payload header is truncated");
    Header header;
    std::memcpy(header.magic.data(), buffer.data(), header.magic.size());
    header.version = le16(buffer.data() + 8);
    header.header_size = le16(buffer.data() + 10);
    header.flags = le32(buffer.data() + 12);
    header.zip_offset = le64(buffer.data() + 16);
    header.zip_size = le64(buffer.data() + 24);
    header.metadata_size = le32(buffer.data() + 32);
    header.metadata_crc32 = le32(buffer.data() + 36);
    header.zip_crc32 = le32(buffer.data() + 40);
    header.entry_count = le32(buffer.data() + 44);
    header.created_unix = le64(buffer.data() + 48);
    return header;
}

inline bool safe_zip_name(std::string name, std::string& normalized) {
    std::replace(name.begin(), name.end(), '\\', '/');
    if (name.empty() || name.front() == '/') return false;
    if (name.size() >= 2 && std::isalpha(static_cast<unsigned char>(name[0])) && name[1] == ':') return false;
    if (name.find(':') != std::string::npos) return false;
    normalized.clear();
    size_t start = 0;
    while (start <= name.size()) {
        size_t end = name.find('/', start);
        if (end == std::string::npos) end = name.size();
        std::string part = name.substr(start, end - start);
        if (part.empty()) {
            if (end == name.size()) break;
            return false;
        }
        if (part == "." || part == "..") return false;
        if (!normalized.empty()) normalized += '/';
        normalized += part;
        start = end + 1;
    }
    return !normalized.empty();
}

inline uint32_t file_crc32(const fs::path& path, uint64_t& size) {
    std::ifstream in(path, std::ios::binary);
    if (!in) throw std::runtime_error("Cannot open input file");
    uint32_t state = crc32_begin();
    size = 0;
    std::array<char, 1 << 20> buffer{};
    while (in) {
        in.read(buffer.data(), buffer.size());
        std::streamsize count = in.gcount();
        if (count > 0) {
            state = crc32_update(state, buffer.data(), static_cast<size_t>(count));
            size += static_cast<uint64_t>(count);
        }
    }
    return crc32_end(state);
}

struct CentralEntry {
    std::string name;
    uint32_t crc32 = 0;
    uint64_t size = 0;
    uint64_t offset = 0;
    uint16_t time = 0;
    uint16_t date = 0;
};

class ZipWriter {
public:
    explicit ZipWriter(std::ostream& out) : out_(out), payload_crc_(crc32_begin()) {}

    void add_file(const fs::path& path, const std::string& name) {
        uint64_t size = 0;
        uint32_t crc = file_crc32(path, size);
        auto [time, date] = dos_time_now();
        CentralEntry entry{name, crc, size, zip_size_, time, date};

        raw_u32(0x04034b50);
        raw_u16(20);
        raw_u16(0x0800);
        raw_u16(0);
        raw_u16(time);
        raw_u16(date);
        raw_u32(crc);
        raw_u32(static_cast<uint32_t>(size));
        raw_u32(static_cast<uint32_t>(size));
        raw_u16(static_cast<uint16_t>(name.size()));
        raw_u16(0);
        raw_bytes(name.data(), name.size());

        std::ifstream in(path, std::ios::binary);
        if (!in) throw std::runtime_error("Cannot open input file");
        std::array<char, 1 << 20> buffer{};
        while (in) {
            in.read(buffer.data(), buffer.size());
            std::streamsize count = in.gcount();
            if (count > 0) raw_bytes(buffer.data(), static_cast<size_t>(count));
        }

        central_.push_back(std::move(entry));
        ++entry_count_;
    }

    void finish() {
        uint64_t central_offset = zip_size_;
        for (const auto& entry : central_) {
            raw_u32(0x02014b50);
            raw_u16(20);
            raw_u16(20);
            raw_u16(0x0800);
            raw_u16(0);
            raw_u16(entry.time);
            raw_u16(entry.date);
            raw_u32(entry.crc32);
            raw_u32(static_cast<uint32_t>(entry.size));
            raw_u32(static_cast<uint32_t>(entry.size));
            raw_u16(static_cast<uint16_t>(entry.name.size()));
            raw_u16(0);
            raw_u16(0);
            raw_u16(0);
            raw_u16(0);
            raw_u32(0);
            raw_u32(static_cast<uint32_t>(entry.offset));
            raw_bytes(entry.name.data(), entry.name.size());
        }
        uint64_t central_size = zip_size_ - central_offset;

        raw_u32(0x06054b50);
        raw_u16(0);
        raw_u16(0);
        raw_u16(static_cast<uint16_t>(central_.size()));
        raw_u16(static_cast<uint16_t>(central_.size()));
        raw_u32(static_cast<uint32_t>(central_size));
        raw_u32(static_cast<uint32_t>(central_offset));
        raw_u16(0);

        zip_crc32_ = crc32_end(payload_crc_);
    }

    uint64_t zip_size() const { return zip_size_; }
    uint32_t zip_crc32() const { return zip_crc32_; }
    uint32_t entry_count() const { return entry_count_; }

private:
    void raw_bytes(const void* data, size_t length) {
        out_.write(static_cast<const char*>(data), static_cast<std::streamsize>(length));
        if (!out_) throw std::runtime_error("Failed to write payload");
        payload_crc_ = crc32_update(payload_crc_, data, length);
        zip_size_ += length;
    }
    void raw_u16(uint16_t value) {
        std::array<uint8_t, 2> buffer{};
        put_le16(buffer.data(), value);
        raw_bytes(buffer.data(), buffer.size());
    }
    void raw_u32(uint32_t value) {
        std::array<uint8_t, 4> buffer{};
        put_le32(buffer.data(), value);
        raw_bytes(buffer.data(), buffer.size());
    }

    static std::pair<uint16_t, uint16_t> dos_time_now() {
        std::time_t now = std::time(nullptr);
        std::tm* local = std::localtime(&now);
        if (local == nullptr) return {0, 0};
        uint16_t time = static_cast<uint16_t>((local->tm_hour << 11) | (local->tm_min << 5) | (local->tm_sec / 2));
        uint16_t date = static_cast<uint16_t>(((local->tm_year - 80) << 9) | ((local->tm_mon + 1) << 5) | local->tm_mday);
        return {time, date};
    }

    std::ostream& out_;
    uint32_t payload_crc_;
    uint64_t zip_size_ = 0;
    uint32_t zip_crc32_ = 0;
    uint32_t entry_count_ = 0;
    std::vector<CentralEntry> central_;
};

} // namespace payload
