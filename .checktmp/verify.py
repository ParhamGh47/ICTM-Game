import glob, os, re, subprocess

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))

print('=== line endings ===')
for t in ['Assets/Scripts/UI/ButtonFocusEffect.cs', 'Assets/Scripts/UI/ButtonFocusEffect.cs.meta']:
    raw = open(t, 'rb').read()
    lf, crlf = raw.count(b'\n'), raw.count(b'\r\n')
    if lf != crlf:
        open(t, 'wb').write(raw.decode('utf-8').replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))
        raw = open(t, 'rb').read()
        print(' %s -> converted (CRLF %d / LF %d)' % (t, raw.count(b'\r\n'), raw.count(b'\n')))
    else:
        print(' %s ok' % t)

print()
print('=== guid uniqueness ===')
for g in ['7b3d9f2e5a1c48e6b0f7c4a83d91e5b2', '5e2f8a1c9b6d4e37a0c5b8d21f4e6a93']:
    hits = [m for m in glob.glob('Assets/**/*.meta', recursive=True) if g in open(m, errors='ignore').read()]
    print(' ', g, '->', hits)

print()
print('=== compile ===')
U = 'D:/Unity/Unity 2022.1.24f1/Editor/Data'
CSC = sorted(glob.glob('C:/Program Files/dotnet/sdk/*/Roslyn/bincore/csc.dll'))[-1]
refs = [U + '/NetStandard/ref/2.1.0/netstandard.dll']
refs += sorted(glob.glob(U + '/NetStandard/compat/2.1.0/shims/*.dll'))
refs += sorted(glob.glob(U + '/NetStandard/compat/2.1.0/shims/*/*.dll'))
refs += sorted(glob.glob(U + '/NetStandard/Extensions/2.0.0/*.dll'))
refs += sorted(glob.glob(U + '/Managed/UnityEngine/*.dll'))
for want in ['UnityEngine.UI.dll', 'Unity.TextMeshPro.dll']:
    found = [f for f in sorted(glob.glob('Library/Bee/artifacts/*/' + want)) if 'Editor' not in f]
    refs.append(os.path.abspath(found[-1]))

sources = (sorted(glob.glob('Assets/Scripts/UI/*.cs'))
           + sorted(glob.glob('Assets/Scripts/UI/Main Menu/*.cs'))
           + sorted(glob.glob('Assets/Scripts/UI/Pause/*.cs')))
for s in sources:
    assert os.path.exists(s), s

os.makedirs('.checktmp/out', exist_ok=True)
with open('.checktmp/compile.rsp', 'w', newline='\n') as fh:
    fh.write('-nologo\n-target:library\n-out:.checktmp/out/Check.dll\n')
    fh.write('-define:UNITY_2022_1_OR_NEWER;UNITY_EDITOR\n')
    for r in refs:
        fh.write('-r:"%s"\n' % os.path.abspath(r))
    for s in sources:
        fh.write('"%s"\n' % os.path.abspath(s))

print('  sources:', ', '.join(os.path.basename(s) for s in sources))
result = subprocess.run(['dotnet', CSC, '@' + os.path.abspath('.checktmp/compile.rsp')],
                        capture_output=True, text=True)
print(result.stdout.strip())
print(result.stderr.strip())
print('COMPILE EXIT', result.returncode)
