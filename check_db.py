import sqlite3, os
db_path = os.path.expandvars(r'%LOCALAPPDATA%\OnHouseLocal\onhouse_v2.db')
print('db_path:', db_path)
conn = sqlite3.connect(db_path)
c = conn.cursor()
c.execute("SELECT name FROM sqlite_master WHERE type='table'")
print('Tables:', c.fetchall())

try:
    c.execute('SELECT UserId, NaverId, AgencyName, RealtorId, UpdatedAt FROM RealtorSettings')
    print('RealtorSettings:', c.fetchall())
except Exception as e:
    print('RealtorSettings error:', e)

try:
    c.execute('SELECT Id, ArticleNumber, ArticleName, PriceDisplay, Address FROM NaverListings')
    rows = c.fetchall()
    print('NaverListings count:', len(rows))
    for r in rows[:5]:
        print('  ', r)
except Exception as e:
    print('NaverListings error:', e)
