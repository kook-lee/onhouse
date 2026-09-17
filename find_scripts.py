with open(r'C:\Users\kook_lee\AppData\Local\Temp\home.html', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

import re
scripts = re.findall(r'<script[^>]+src=["\']([^"\']+)["\']', text)
for s in scripts:
    print(s)
