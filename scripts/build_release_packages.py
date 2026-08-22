import os
import sys
import subprocess
import shutil
import zipfile

repo_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
build_dir = os.path.join(repo_dir, r"bin\x64\Release\net8.0-windows10.0.19041.0\win-x64")
dist_dir = os.path.join(repo_dir, "dist")
iscc_exe = r"C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
version = "2.1.0"

os.makedirs(dist_dir, exist_ok=True)

print("=== 1. Building Release x64 ===")
dotnet_exe = shutil.which("dotnet") or r"C:\Program Files\dotnet\dotnet.exe"
build_cmd = [dotnet_exe, "build", os.path.join(repo_dir, "XTimelineViewer.csproj"), "-c", "Release", "-p:Platform=x64"]
res = subprocess.run(build_cmd, cwd=repo_dir)
if res.returncode != 0:
    print("Build failed.")
    sys.exit(1)

# 拡張機能の確認とコピー
ext_src = os.path.join(repo_dir, "extensions", "xtv-translator")
ext_dst = os.path.join(build_dir, "extensions", "xtv-translator")
if os.path.exists(ext_src):
    os.makedirs(ext_dst, exist_ok=True)
    for item in os.listdir(ext_src):
        s = os.path.join(ext_src, item)
        d = os.path.join(ext_dst, item)
        if os.path.isfile(s):
            shutil.copy2(s, d)
        elif os.path.isdir(s):
            shutil.copytree(s, d, dirs_exist_ok=True)
    print("Extensions verified in build directory.")

print("=== 2. Creating Portable ZIP Package ===")
zip_filename = f"XTimelineViewer-Kotsume-v{version}-win-x64-Portable.zip"
zip_path = os.path.join(dist_dir, zip_filename)

with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
    for root, dirs, files in os.walk(build_dir):
        for f in files:
            full_path = os.path.join(root, f)
            rel_path = os.path.relpath(full_path, build_dir)
            zf.write(full_path, os.path.join(f"XTimelineViewer-Kotsume-v{version}", rel_path))

print(f"Created Portable ZIP: {zip_path} ({os.path.getsize(zip_path) / (1024*1024):.2f} MB)")

print("=== 3. Creating Installer EXE Package (Inno Setup) ===")
iss_path = os.path.join(repo_dir, "scripts", "installer.iss")
if os.path.exists(iscc_exe):
    res_iscc = subprocess.run([iscc_exe, iss_path], cwd=os.path.dirname(iss_path))
    if res_iscc.returncode == 0:
        installer_path = os.path.join(dist_dir, f"XTimelineViewer-Kotsume-v{version}-Setup.exe")
        print(f"Created Installer EXE: {installer_path} ({os.path.getsize(installer_path) / (1024*1024):.2f} MB)")
    else:
        print("Inno Setup compilation failed.")
else:
    print("ISCC.exe not found. Skipped installer.")

print("=== 4. Updating Local WinGet Installation ===")
winget_dir = os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WinGet\Packages\daruyanagi.XTimelineViewer_Microsoft.Winget.Source_8wekyb3d8bbwe")
if os.path.exists(winget_dir):
    for root, dirs, files in os.walk(build_dir):
        rel = os.path.relpath(root, build_dir)
        target_root = os.path.join(winget_dir, rel)
        os.makedirs(target_root, exist_ok=True)
        for f in files:
            shutil.copy2(os.path.join(root, f), os.path.join(target_root, f))
    print("Local WinGet directory updated successfully.")

print("=== All Release Packages Successfully Generated! ===")
