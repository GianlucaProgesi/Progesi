# Crea un DB EF da StringTable (se implementato nel tool)
.\Progesi.EF.Tool.exe export "C:\Temp\Progesi_EF.db"

# Importa il DB EF in RHINO (se il tool e' lanciato dentro un contesto che espone StringTable)
.\Progesi.EF.Tool.exe import "C:\Temp\Progesi_EF.db"
