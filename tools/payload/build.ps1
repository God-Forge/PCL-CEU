$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot
try {
    & g++ -std=c++17 -O2 -static -s -o encode.exe encode.cpp -lshell32
    if ($LASTEXITCODE -ne 0) { throw "encode.exe build failed" }

    & g++ -std=c++17 -O2 -static -s -o decode.exe decode.cpp -lshell32
    if ($LASTEXITCODE -ne 0) { throw "decode.exe build failed" }

    Get-ChildItem encode.exe, decode.exe | Select-Object Name, Length
} finally {
    Pop-Location
}
