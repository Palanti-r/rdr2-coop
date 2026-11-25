"""Build the Windows settings app and both mod DLLs into one ZIP."""

import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from urllib.request import Request, urlopen
from zipfile import ZIP_DEFLATED, ZipFile


TOOL = Path(__file__).resolve().parent
ROOT = TOOL.parents[1]
SHDN_URL = (
    "https://github.com/Halen84/ScriptHookRDR2DotNet-V2/releases/download/"
    "2.2.0/ScriptHookRDR2DotNet-V2.rar"
)
SHDN_ARCHIVE_SHA256 = "61682e6236618849fa96a5f4a305681c838120158857db57d72da5311f7e97f8"
SHDN_API_SHA256 = "bfb7607d94b2de7f8530289377f163caad81098afc1fb7ee86191810a6cc6bba"


def run(*command):
    subprocess.run(command, cwd=ROOT, check=True)


def verify_sha256(path, expected):
    if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
        raise SystemExit(f"SHA-256 check failed for {path}. Expected the ScriptHookRDR2DotNet V2 2.2.0 file.")


def prepare_legacy_reference():
    reference = ROOT / "lib" / "ScriptHookRDRNetAPI.dll"
    if reference.is_file():
        verify_sha256(reference, SHDN_API_SHA256)
        return

    bsdtar = shutil.which("bsdtar")
    seven_zip = shutil.which("7z")
    if not bsdtar and not seven_zip and os.name == "nt":
        installed = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "7-Zip" / "7z.exe"
        if installed.is_file():
            seven_zip = str(installed)
    if not bsdtar and not seven_zip:
        raise SystemExit("Install bsdtar or 7-Zip, or put the 2.2.0 ScriptHookRDRNetAPI.dll in lib/.")

    with tempfile.TemporaryDirectory() as temporary:
        folder = Path(temporary)
        archive = folder / "ScriptHookRDR2DotNet-V2.rar"
        request = Request(SHDN_URL, headers={"User-Agent": "RDR2Coop-Builder"})
        with urlopen(request, timeout=60) as response, archive.open("wb") as output:
            shutil.copyfileobj(response, output)
        verify_sha256(archive, SHDN_ARCHIVE_SHA256)
        if bsdtar:
            run(bsdtar, "-xf", archive, "-C", folder, reference.name)
        else:
            run(seven_zip, "e", archive, f"-o{folder}", reference.name)
        extracted = folder / reference.name
        verify_sha256(extracted, SHDN_API_SHA256)
        reference.parent.mkdir(exist_ok=True)
        shutil.copyfile(extracted, reference)


def build_package():
    prepare_legacy_reference()
    for target in ("net9.0", "net48"):
        run("dotnet", "build", "src/RDR2Coop.csproj", "-c", "Release", f"-p:TargetFrameworks={target}")
    run("dotnet", "build", "tools/CharacterPicker/RDR2CoopCharacterPicker.csproj", "-c", "Release")

    picker = TOOL / "bin" / "Release" / "net48"
    mod = ROOT / "src" / "bin" / "Release"
    files = {
        "RDR2CoopCharacterPicker.exe": picker / "RDR2CoopCharacterPicker.exe",
        "RDR2CoopCharacterPicker.exe.config": picker / "RDR2CoopCharacterPicker.exe.config",
        "Standard/RDR2Coop.dll": mod / "net9.0" / "RDR2Coop.dll",
        "Legacy/RDR2Coop.dll": mod / "net48" / "RDR2Coop.dll",
        "README.txt": TOOL / "INSTALL.txt",
        "VERSION.txt": ROOT / "VERSION",
        "LICENSE": ROOT / "LICENSE",
        "NOTICE": ROOT / "NOTICE",
    }
    package = ROOT / "dist" / "RDR2-Coop-Settings.zip"
    package.parent.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(dir=package.parent) as temporary:
        pending = Path(temporary) / package.name
        with ZipFile(pending, "w", ZIP_DEFLATED) as archive:
            for name, path in files.items():
                archive.write(path, name)
        pending.replace(package)
    print(f"Built {package.relative_to(ROOT)}")


if __name__ == "__main__":
    build_package()
