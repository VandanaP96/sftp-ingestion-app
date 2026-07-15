@echo off
REM ================================================================================================
REM Build ShareNormalizer.exe WITHOUT Visual Studio, using the in-box .NET Framework C# compiler.
REM No SDK, no NuGet, no internet - csc.exe ships with the .NET Framework that is already on Windows.
REM
REM This is the fallback path. The normal way is to open ShareNormalizer.sln in Visual Studio and
REM build (which produces src\ShareNormalizer\bin\<Config>\ShareNormalizer.exe). Use this script on a
REM build box that has no Visual Studio / .NET SDK.
REM
REM Output:  bin\ShareNormalizer.exe   (a single, self-contained .exe) + a copy of normalizer.conf
REM Deploy:  copy ONLY bin\ShareNormalizer.exe to the Windows Server (source stays here).
REM          normalizer.conf is optional; the exe also takes everything via command-line args.
REM
REM The produced exe targets .NET Framework 4.x, which is in-box on Windows Server 2016/2019/2022,
REM so it runs on the server with nothing to install.
REM ================================================================================================

setlocal

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo ERROR: csc.exe not found. Install the .NET Framework 4.x ^(in-box on Windows Server^).
    exit /b 1
)

if not exist bin mkdir bin

echo Using compiler: %CSC%

"%CSC%" ^
/nologo ^
/optimize+ ^
/platform:anycpu ^
/target:exe ^
/out:bin\ShareNormalizer.exe ^
/r:System.Data.dll ^
/r:System.IO.Compression.dll ^
/r:System.Xml.Linq.dll ^
/r:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll" ^
/r:lib\Snowflake.Data.dll ^
src\ShareNormalizer\*.cs ^
src\ShareNormalizer\Properties\*.cs ^
src\ShareNormalizer\Snowflake\Configuration\*.cs ^
src\ShareNormalizer\Snowflake\Constants\*.cs ^

src\ShareNormalizer\Snowflake\Helpers\*.cs ^
src\ShareNormalizer\Snowflake\Infrastructure\*.cs ^

src\ShareNormalizer\Snowflake\Mappers\*.cs ^
src\ShareNormalizer\Snowflake\Models\*.cs ^
src\ShareNormalizer\Snowflake\Repository\*.cs ^
src\ShareNormalizer\Snowflake\Services\*.cs

if errorlevel 1 (
    echo.
    echo BUILD FAILED.
    exit /b 1
)

copy /Y src\ShareNormalizer\normalizer.conf bin\normalizer.conf >nul
copy /Y lib\*.dll bin\ >nul

echo.
echo BUILD OK -^> bin\ShareNormalizer.exe
echo.
echo Ship ONLY:
echo    bin\ShareNormalizer.exe
echo    normalizer.conf
echo.
echo Run:
echo    ShareNormalizer.exe --source "\\server\share\NewBusinessProcessing" --out "D:\meduit\normalized"

endlocal