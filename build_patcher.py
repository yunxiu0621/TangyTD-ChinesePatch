# -*- coding: utf-8 -*-
"""
Tangy TD 简体中文补丁工具构建脚本（项目自包含）
- payload 位于 ./payload/{patch,original}（各 11 个文件，尺寸断言）
- 22 个文件 zlib(raw deflate) 压缩后内嵌为资源，系统自带 csc 编译 src/Patcher.cs + 生成的 build/Manifest.cs
- 输出：dist/TangyTD_简体中文补丁_v{版本}.exe（文件名带版本号）
- 同时打包 dist/TangyTD_简体中文补丁_v{版本}.zip（exe + 使用说明 + 字体 OFL 许可）
用法：python build_patcher.py
"""
import os
import subprocess
import sys
import zipfile
import zlib

ROOT = os.path.dirname(os.path.abspath(__file__))
PAYLOAD = os.path.join(ROOT, 'payload')
SRC = os.path.join(ROOT, 'src', 'Patcher.cs')
BUILD = os.path.join(ROOT, 'build')
DIST = os.path.join(ROOT, 'dist')
CSC = r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
VERSION = '1.0.393'          # 支持的游戏版本（同时是 exe 文件名/程序集版本）

REL = [
    r'TangyTD.exe',
    r'game.dll',
    r'credits.txt',
    r'assets\fonts\alagard.ttf',
    r'assets\fonts\NameHereCondensed.ttf',
    r'assets\fonts\CelticTime.ttf',
    r'assets\fonts\AKDPixel.ttf',
    r'assets\fonts\PixeloidMono.ttf',
    r'assets\fonts\TinyUnicode.ttf',
    r'assets\shaders\dx\gpu.hlsl',
    r'assets\shaders\dx\compiled\gpu.ps',
]

EXPECT_ORIG = {r'TangyTD.exe': 4357632, r'game.dll': 6387200}
EXPECT_PATCHED = {r'TangyTD.exe': 19040768, r'game.dll': 6415360}


def cs_str(s):
    return '"%s"' % s.replace('\\', '\\\\').replace('"', '\\"')


def build_group(kind, prefix):
    """压缩 payload/<kind>/ 下 11 个文件 -> build/<prefix>N.bin；返回 C# Entry 行列表"""
    lines = []
    os.makedirs(BUILD, exist_ok=True)
    for i, rel in enumerate(REL):
        src = os.path.join(PAYLOAD, kind, rel)
        data = open(src, 'rb').read()
        size = len(data)
        expect = EXPECT_ORIG if kind == 'original' else EXPECT_PATCHED
        if rel in expect:
            assert size == expect[rel], f'{src} 大小 {size} != {expect[rel]}'
        co = zlib.compressobj(9, zlib.DEFLATED, -15)   # raw deflate，C# DeflateStream 可直接解
        comp = co.compress(data) + co.flush()
        rid = f'{prefix}{i}'
        with open(os.path.join(BUILD, rid + '.bin'), 'wb') as f:
            f.write(comp)
        lines.append(f'            new Entry("{rid}", {cs_str(rel)}, {size}),')
        print(f'  {kind}/{rel}: {size} -> {len(comp)} B')
    return lines


def main():
    print('[1/4] 压缩 payload …')
    patched = build_group('patch', 'P')
    original = build_group('original', 'O')

    print('[2/4] 生成 Manifest.cs …')
    manifest = f'''// 本文件由 build_patcher.py 自动生成，勿手改
namespace TangyPatch
{{
    public struct Entry
    {{
        public string Res; public string Path; public long Size;
        public Entry(string r, string p, long s) {{ Res = r; Path = p; Size = s; }}
    }}

    static class Manifest
    {{
        public const string Version = "{VERSION}";
        public const long ORIG_EXE = {EXPECT_ORIG[r'TangyTD.exe']};
        public const long ORIG_DLL = {EXPECT_ORIG[r'game.dll']};
        public const long PATCHED_EXE = {EXPECT_PATCHED[r'TangyTD.exe']};
        public const long PATCHED_DLL = {EXPECT_PATCHED[r'game.dll']};

        public static readonly Entry[] Patched = new Entry[] {{
{chr(10).join(patched)}
        }};

        public static readonly Entry[] Original = new Entry[] {{
{chr(10).join(original)}
        }};
    }}
}}
'''
    with open(os.path.join(BUILD, 'Manifest.cs'), 'w', encoding='utf-8-sig') as f:
        f.write(manifest)

    print('[3/4] csc 编译 …')
    os.makedirs(DIST, exist_ok=True)
    exe = os.path.join(DIST, f'TangyTD_简体中文补丁_v{VERSION}.exe')
    res_args = []
    for i in range(len(REL)):
        res_args.append(f'/resource:{os.path.join(BUILD, f"P{i}.bin")},P{i}')
        res_args.append(f'/resource:{os.path.join(BUILD, f"O{i}.bin")},O{i}')
    cmd = [
        CSC, '/nologo', '/target:winexe', '/platform:anycpu',
        '/codepage:65001', '/optimize+',
        '/r:System.dll', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
        '/r:System.IO.Compression.dll',
        f'/out:{exe}',
        SRC,
        os.path.join(BUILD, 'Manifest.cs'),
    ] + res_args
    r = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                       text=True, encoding='utf-8', errors='replace', shell=False, cwd=ROOT)
    print(f'csc rc={r.returncode}')
    if r.stdout.strip():
        print(r.stdout.strip())
    if r.returncode != 0:
        print(r.stderr.strip())
        sys.exit(f'csc 失败 rc={r.returncode}')
    print(f'[OK] {exe} ({os.path.getsize(exe):,} B)')

    print('[4/4] 打包分发 zip …')
    zipp = os.path.join(DIST, f'TangyTD_简体中文补丁_v{VERSION}.zip')
    with zipfile.ZipFile(zipp, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        z.write(exe, os.path.basename(exe))
        readme = os.path.join(ROOT, '使用说明.txt')
        if os.path.exists(readme):
            z.write(readme, '使用说明.txt')
        for name in sorted(os.listdir(os.path.join(ROOT, 'licenses'))):
            z.write(os.path.join(ROOT, 'licenses', name), f'字体授权-{name}')
    print(f'[OK] {zipp} ({os.path.getsize(zipp):,} B)')


if __name__ == '__main__':
    main()
