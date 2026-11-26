"""Build and verify a complete Windows co-op package with the settings app."""

from copy import copy
import hashlib
from pathlib import Path
import shutil
import subprocess
import tempfile
from urllib.request import Request, urlopen
from zipfile import ZIP_DEFLATED, ZipFile


TOOL = Path(__file__).resolve().parent
ROOT = TOOL.parents[1]
PACKAGE_ROOT = "RDR2-Coop/"
BASE_VERSION = "0.9.0-beta.8.5"
BASE_NAME = f"RDR2-Coop-Windows-{BASE_VERSION}.zip"
BASE_URL = f"https://github.com/BigCheeseThe3st/rdr2-coop/releases/download/v{BASE_VERSION}/{BASE_NAME}"
BASE_SHA256 = "a1f6c4eb737ff7e5729298ddd4230638188fd8368aa0f6fe8035c1f112524c16"


def run(*command):
    subprocess.run(command, cwd=ROOT, check=True)


def verify_sha256(path, expected):
    if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
        raise SystemExit(f"SHA-256 check failed for {path}")


def download(url, path, expected):
    request = Request(url, headers={"User-Agent": "RDR2Coop-Builder"})
    with urlopen(request, timeout=90) as response, path.open("wb") as output:
        shutil.copyfileobj(response, output)
    verify_sha256(path, expected)


def prepare_base():
    version = (ROOT / "VERSION").read_text().strip()
    if version != BASE_VERSION:
        raise SystemExit(f"VERSION is {version}. Pin the matching official Windows ZIP and SHA-256 first.")

    base = ROOT / "build" / BASE_NAME
    base.parent.mkdir(exist_ok=True)
    if base.is_file():
        verify_sha256(base, BASE_SHA256)
    else:
        with tempfile.TemporaryDirectory(dir=base.parent) as temporary:
            downloaded = Path(temporary) / BASE_NAME
            download(BASE_URL, downloaded, BASE_SHA256)
            downloaded.replace(base)
    return base


def prepare_legacy_reference(base):
    reference = ROOT / "lib" / "ScriptHookRDRNetAPI.dll"
    with ZipFile(base) as upstream:
        data = upstream.read(PACKAGE_ROOT + "scripthookdotnet/ScriptHookRDRNetAPI.dll")
    if reference.is_file():
        if reference.read_bytes() != data:
            raise SystemExit(f"{reference} differs from the legacy API in the official ZIP.")
        return

    reference.parent.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(dir=reference.parent) as temporary:
        extracted = Path(temporary) / reference.name
        extracted.write_bytes(data)
        extracted.replace(reference)


def write_package(base, output, replacements, additions):
    with ZipFile(base) as upstream:
        upstream_names = set(upstream.namelist())
        if (upstream.read(PACKAGE_ROOT + "VERSION.txt").decode().strip() != BASE_VERSION
                or not replacements.keys() <= upstream_names or additions.keys() & upstream_names):
            raise SystemExit("The official ZIP layout does not match the expected Windows package.")

        with ZipFile(output, "w", ZIP_DEFLATED) as package:
            for entry in upstream.infolist():
                if entry.filename in replacements:
                    package.writestr(copy(entry), replacements[entry.filename].read_bytes())
                else:
                    with upstream.open(entry) as source, package.open(copy(entry), "w") as destination:
                        shutil.copyfileobj(source, destination)
            for name, path in additions.items():
                package.write(path, name)

        overridden = {**replacements, **additions}
        with ZipFile(output) as package:
            if package.namelist() != upstream.namelist() + list(additions):
                raise SystemExit("The output ZIP is missing files or has unexpected entries.")
            for name in package.namelist():
                expected = overridden[name].read_bytes() if name in overridden else upstream.read(name)
                if package.read(name) != expected:
                    raise SystemExit(f"File verification failed for {name}")
        preserved = len(upstream_names) - len(replacements)
        print(f"Verified {preserved} unchanged upstream entries, {len(replacements)} replacements, and {len(additions)} additions.")


def build_package():
    base = prepare_base()
    prepare_legacy_reference(base)
    for target in ("net9.0", "net48"):
        run("dotnet", "build", "src/RDR2Coop.csproj", "-c", "Release", f"-p:TargetFrameworks={target}")
    run("dotnet", "build", "tools/CharacterPicker/RDR2CoopCharacterPicker.csproj", "-c", "Release")
    picker = TOOL / "bin" / "Release" / "net48"
    mod = ROOT / "src" / "bin" / "Release"
    ini = ROOT / "RDR2Coop.ini"
    replacements = {
        "RDR2Coop/RDR2Coop.dll": mod / "net9.0" / "RDR2Coop.dll",
        "shdn/scripts/RDR2Coop.dll": mod / "net48" / "RDR2Coop.dll",
        "scripts/RDR2Coop.ini": ini,
        "shdn/scripts/RDR2Coop.ini": ini,
    }
    additions = {
        "RDR2CoopCharacterPicker.exe": picker / "RDR2CoopCharacterPicker.exe",
        "RDR2CoopCharacterPicker.exe.config": picker / "RDR2CoopCharacterPicker.exe.config",
        "README-SETTINGS.txt": TOOL / "INSTALL.txt",
        "LICENSE-FORK.txt": ROOT / "LICENSE",
        "NOTICE-FORK.txt": ROOT / "NOTICE",
    }
    package = ROOT / "dist" / "RDR2-Coop-Settings.zip"
    package.parent.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(dir=package.parent) as temporary:
        pending = Path(temporary) / package.name
        write_package(
            base, pending,
            {PACKAGE_ROOT + name: path for name, path in replacements.items()},
            {PACKAGE_ROOT + name: path for name, path in additions.items()},
        )
        pending.replace(package)
    print(f"Built {package.relative_to(ROOT)}")


if __name__ == "__main__":
    build_package()
