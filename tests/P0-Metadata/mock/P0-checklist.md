# P0 â€“ Metadata (Mock)

## MetIn
- Create: Run=True, Act='Create', By='GM', Info='This is a test', Ref=<url/path>, Snip=<base64/url/path>
  - atteso: Id=1, Hash=<non vuoto>, Info='OK'
- Update: Run=True, Act='Update', Id=1, ... 
  - atteso: Id=1, Info='OK'
- Delete: Run=True, Act='Delete', Id=1
  - atteso: Id=1, Info='OK'

## MetOut
- Get by Hash: Run=True, Hash='mock-00000001'
  - atteso: Id=1, Info='OK', Refs e Snips valorizzati, LastModified='2025-09-29T01:00:00Z'
- Get by Id:   Run=True, Id=2 (Hash vuoto)
  - atteso: Id=2, Info='OK', Refs e Snips valorizzati
