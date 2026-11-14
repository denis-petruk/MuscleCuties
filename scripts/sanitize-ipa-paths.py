#!/usr/bin/env python3
"""Replace build-machine path tokens in an app bundle, preserving file lengths."""
import os
import re
import stat
import sys

root = sys.argv[1]
if not os.path.isdir(root):
    raise SystemExit(f"not an app bundle: {root}")

skip_names = {'embedded.mobileprovision'}
skip_dirs = {'_CodeSignature'}
user_pattern = re.compile(rb'/Users/[^/\x00\r\n \t]*')
home_pattern = re.compile(rb'/home/[^/\x00\r\n \t]*')
fixed_paths = (b'/private/tmp', b'/var/folders')

def pad(prefix, original):
    return prefix + b'_' * (len(original) - len(prefix))

files = 0
markers = 0
for dirpath, dirnames, filenames in os.walk(root):
    dirnames[:] = [d for d in dirnames if d not in skip_dirs]
    for name in filenames:
        if name in skip_names or name.endswith('.mobileprovision') or name.endswith('.plist'):
            continue
        path = os.path.join(dirpath, name)
        try:
            with open(path, 'rb') as f:
                data = f.read()
        except OSError:
            continue
        count = 0
        data2, n = user_pattern.subn(lambda m: pad(b'/_/', m.group(0)), data)
        count += n
        data2, n = home_pattern.subn(lambda m: pad(b'/_/', m.group(0)), data2)
        count += n
        for token in fixed_paths:
            n = data2.count(token)
            if n:
                data2 = data2.replace(token, pad(b'/_/', token))
                count += n
        if count:
            mode = stat.S_IMODE(os.stat(path).st_mode)
            with open(path, 'wb') as f:
                f.write(data2)
            os.chmod(path, mode)
            files += 1
            markers += count
print(f'sanitized_files={files} path_markers_replaced={markers}')
