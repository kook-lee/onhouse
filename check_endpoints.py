with open(r'C:\Users\kook_lee\AppData\Local\Temp\api.js', 'r', encoding='utf-8', errors='ignore') as f:
    api_text = f.read()

with open(r'C:\Users\kook_lee\AppData\Local\Temp\app.js', 'r', encoding='utf-8', errors='ignore') as f:
    app_text = f.read()

import re
print("=== api.js URLs/endpoints ===")
endpoints = re.findall(r'["\'](/[a-zA-Z0-9_/.-]+)["\']', api_text)
for ep in sorted(set(endpoints)):
    print(ep)

print("\n=== app.js URLs/endpoints ===")
endpoints2 = re.findall(r'["\'](/[a-zA-Z0-9_/.-]+)["\']', app_text)
for ep in sorted(set(endpoints2)):
    print(ep)
