from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]

def read(rel): return (ROOT/rel).read_text(encoding='utf-8')
def write(rel,text): (ROOT/rel).write_text(text,encoding='utf-8',newline='\n')

# LabelPrintSnapshotService: schema + insert through global coordinator; reads use hardened connection string.
rel='src/Partcounter.App/Services/LabelPrintSnapshotService.cs'
text=read(rel)
text=text.replace('private string ConnectionString => $"Data Source={_database.DatabasePath};Cache=Shared";', 'private string ConnectionString => SqliteWriteCoordinator.BuildConnectionString(_database.DatabasePath);',1)
old='''        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS LabelPrintSnapshots (
                PackagingUnitId TEXT PRIMARY KEY,
                TemplateId TEXT NOT NULL,
                TemplateName TEXT NOT NULL,
                TemplateUpdatedAtUtc TEXT NOT NULL,
                DefinitionJson TEXT NOT NULL,
                DefinitionSha256 TEXT NOT NULL,
                CapturedAtUtc TEXT NOT NULL,
                FOREIGN KEY(PackagingUnitId) REFERENCES PackagingUnits(Id)
            );

            CREATE INDEX IF NOT EXISTS IX_LabelPrintSnapshots_CapturedAtUtc
                ON LabelPrintSnapshots(CapturedAtUtc DESC);
            """;
        await command.ExecuteNonQueryAsync();'''
new='''        await _database.ExecuteExclusiveWriteAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS LabelPrintSnapshots (
                    PackagingUnitId TEXT PRIMARY KEY,
                    TemplateId TEXT NOT NULL,
                    TemplateName TEXT NOT NULL,
                    TemplateUpdatedAtUtc TEXT NOT NULL,
                    DefinitionJson TEXT NOT NULL,
                    DefinitionSha256 TEXT NOT NULL,
                    CapturedAtUtc TEXT NOT NULL,
                    FOREIGN KEY(PackagingUnitId) REFERENCES PackagingUnits(Id)
                );

                CREATE INDEX IF NOT EXISTS IX_LabelPrintSnapshots_CapturedAtUtc
                    ON LabelPrintSnapshots(CapturedAtUtc DESC);
                """;
            await command.ExecuteNonQueryAsync();
        });'''
if text.count(old)!=1: raise RuntimeError('snapshot init pattern')
text=text.replace(old,new,1)
old='''        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO LabelPrintSnapshots
                (PackagingUnitId, TemplateId, TemplateName, TemplateUpdatedAtUtc, DefinitionJson, DefinitionSha256, CapturedAtUtc)
            VALUES
                ($ve, $templateId, $templateName, $updated, $json, $hash, $captured);
            """;
        command.Parameters.AddWithValue("$ve", record.Id);
        command.Parameters.AddWithValue("$templateId", template.Id ?? string.Empty);
        command.Parameters.AddWithValue("$templateName", template.Name ?? string.Empty);
        command.Parameters.AddWithValue("$updated", template.UpdatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$captured", capturedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync();'''
new='''        await _database.ExecuteExclusiveWriteAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO LabelPrintSnapshots
                    (PackagingUnitId, TemplateId, TemplateName, TemplateUpdatedAtUtc, DefinitionJson, DefinitionSha256, CapturedAtUtc)
                VALUES
                    ($ve, $templateId, $templateName, $updated, $json, $hash, $captured);
                """;
            command.Parameters.AddWithValue("$ve", record.Id);
            command.Parameters.AddWithValue("$templateId", template.Id ?? string.Empty);
            command.Parameters.AddWithValue("$templateName", template.Name ?? string.Empty);
            command.Parameters.AddWithValue("$updated", template.UpdatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$json", json);
            command.Parameters.AddWithValue("$hash", hash);
            command.Parameters.AddWithValue("$captured", capturedAtUtc.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });'''
if text.count(old)!=1: raise RuntimeError('snapshot insert pattern')
write(rel,text.replace(old,new,1))

# LabelReprintService schema migration through coordinator.
rel='src/Partcounter.App/Services/LabelReprintService.cs'
text=read(rel)
old='''        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS LabelReprintJournal (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PackagingUnitId TEXT NOT NULL,
                ReprintNumber INTEGER NOT NULL,
                PrintedAtUtc TEXT NOT NULL,
                PrinterName TEXT NOT NULL,
                Reason TEXT NOT NULL,
                Successful INTEGER NOT NULL,
                ErrorMessage TEXT NOT NULL DEFAULT '',
                LayoutSource TEXT NOT NULL DEFAULT '',
                FOREIGN KEY(PackagingUnitId) REFERENCES PackagingUnits(Id)
            );

            CREATE INDEX IF NOT EXISTS IX_LabelReprintJournal_PackagingUnitId
                ON LabelReprintJournal(PackagingUnitId, Id DESC);

            CREATE UNIQUE INDEX IF NOT EXISTS UX_LabelReprintJournal_Number
                ON LabelReprintJournal(PackagingUnitId, ReprintNumber);
            """;
        await command.ExecuteNonQueryAsync();
        await EnsureColumnAsync(connection, "LabelReprintJournal", "LayoutSource", "TEXT NOT NULL DEFAULT ''");'''
new='''        await _database.ExecuteExclusiveWriteAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS LabelReprintJournal (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PackagingUnitId TEXT NOT NULL,
                    ReprintNumber INTEGER NOT NULL,
                    PrintedAtUtc TEXT NOT NULL,
                    PrinterName TEXT NOT NULL,
                    Reason TEXT NOT NULL,
                    Successful INTEGER NOT NULL,
                    ErrorMessage TEXT NOT NULL DEFAULT '',
                    LayoutSource TEXT NOT NULL DEFAULT '',
                    FOREIGN KEY(PackagingUnitId) REFERENCES PackagingUnits(Id)
                );

                CREATE INDEX IF NOT EXISTS IX_LabelReprintJournal_PackagingUnitId
                    ON LabelReprintJournal(PackagingUnitId, Id DESC);

                CREATE UNIQUE INDEX IF NOT EXISTS UX_LabelReprintJournal_Number
                    ON LabelReprintJournal(PackagingUnitId, ReprintNumber);
                """;
            await command.ExecuteNonQueryAsync();
            await EnsureColumnAsync(connection, "LabelReprintJournal", "LayoutSource", "TEXT NOT NULL DEFAULT ''");
        });'''
if text.count(old)!=1: raise RuntimeError('reprint init pattern')
write(rel,text.replace(old,new,1))

# LabelTemplateService schema + default seed in coordinator. Save/Delete were already coordinated.
rel='src/Partcounter.App/Services/LabelTemplateService.cs'
text=read(rel)
old='''        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS LabelTemplates (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                WidthMm REAL NOT NULL,
                HeightMm REAL NOT NULL,
                IsDefault INTEGER NOT NULL DEFAULT 0,
                AssignedArticleNumber TEXT NULL,
                DefinitionJson TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_LabelTemplates_Article
                ON LabelTemplates(AssignedArticleNumber);
            """;
        await command.ExecuteNonQueryAsync();

        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM LabelTemplates;";
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        if (count == 0)
            await SaveTemplateAsync(CreateLegacyCompatibleDefaultTemplate());'''
new='''        var needsDefault = await SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS LabelTemplates (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    WidthMm REAL NOT NULL,
                    HeightMm REAL NOT NULL,
                    IsDefault INTEGER NOT NULL DEFAULT 0,
                    AssignedArticleNumber TEXT NULL,
                    DefinitionJson TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_LabelTemplates_Article
                    ON LabelTemplates(AssignedArticleNumber);
                """;
            await command.ExecuteNonQueryAsync();
            var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM LabelTemplates;";
            return Convert.ToInt32(await countCommand.ExecuteScalarAsync()) == 0;
        });
        if (needsDefault)
            await SaveTemplateAsync(CreateLegacyCompatibleDefaultTemplate());'''
if text.count(old)!=1: raise RuntimeError('template init pattern')
write(rel,text.replace(old,new,1))

# CommissioningDatabaseService: same DB, so all writes/schema use coordinator; reads use same hardened connection string.
rel='src/Partcounter.App/Services/CommissioningDatabaseService.cs'
text=read(rel)
text=text.replace('''    private readonly string _connectionString;

    public CommissioningDatabaseService(string databasePath)
    {
        _connectionString = $"Data Source={databasePath};Cache=Shared";
    }''','''    private readonly string _databasePath;
    private string ConnectionString => SqliteWriteCoordinator.BuildConnectionString(_databasePath);

    public CommissioningDatabaseService(string databasePath)
    {
        _databasePath = databasePath;
    }''',1)
text=text.replace('new SqliteConnection(_connectionString)','new SqliteConnection(ConnectionString)')
old='''        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS CommissioningProfiles (
                MachineNumber INTEGER PRIMARY KEY,
                LogoOrderNumber TEXT NOT NULL,
                LogoType TEXT NOT NULL,
                SupplyVoltage TEXT NOT NULL,
                CycleInput TEXT NOT NULL,
                CycleSignal TEXT NOT NULL,
                ValveOutput TEXT NOT NULL,
                ValveVoltage TEXT NOT NULL,
                UseInterfaceRelay INTEGER NOT NULL,
                EndPositionMonitoring INTEGER NOT NULL,
                EndPositionInput TEXT NOT NULL,
                DefaultValvePulseMs INTEGER NOT NULL,
                ReleaseState INTEGER NOT NULL,
                Notes TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CommissioningChecks (
                MachineNumber INTEGER NOT NULL,
                CheckCode TEXT NOT NULL,
                Result INTEGER NOT NULL,
                Note TEXT NOT NULL,
                CheckedAtUtc TEXT NULL,
                PRIMARY KEY (MachineNumber, CheckCode)
            );

            CREATE INDEX IF NOT EXISTS IX_CommissioningProfiles_ReleaseState
                ON CommissioningProfiles(ReleaseState);
            """;
        await command.ExecuteNonQueryAsync();'''
new='''        await SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS CommissioningProfiles (
                    MachineNumber INTEGER PRIMARY KEY,
                    LogoOrderNumber TEXT NOT NULL,
                    LogoType TEXT NOT NULL,
                    SupplyVoltage TEXT NOT NULL,
                    CycleInput TEXT NOT NULL,
                    CycleSignal TEXT NOT NULL,
                    ValveOutput TEXT NOT NULL,
                    ValveVoltage TEXT NOT NULL,
                    UseInterfaceRelay INTEGER NOT NULL,
                    EndPositionMonitoring INTEGER NOT NULL,
                    EndPositionInput TEXT NOT NULL,
                    DefaultValvePulseMs INTEGER NOT NULL,
                    ReleaseState INTEGER NOT NULL,
                    Notes TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS CommissioningChecks (
                    MachineNumber INTEGER NOT NULL,
                    CheckCode TEXT NOT NULL,
                    Result INTEGER NOT NULL,
                    Note TEXT NOT NULL,
                    CheckedAtUtc TEXT NULL,
                    PRIMARY KEY (MachineNumber, CheckCode)
                );

                CREATE INDEX IF NOT EXISTS IX_CommissioningProfiles_ReleaseState
                    ON CommissioningProfiles(ReleaseState);
                """;
            await command.ExecuteNonQueryAsync();
        });'''
if text.count(old)!=1: raise RuntimeError('commission init pattern')
text=text.replace(old,new,1)
# Replace write method bodies by coordinator while keeping SQL payload intact through regex.
def wrap_method(text, signature):
    start=text.find(signature)
    if start<0: raise RuntimeError('missing '+signature)
    brace=text.find('{',start)
    # crude balanced braces
    depth=0; end=None
    for i in range(brace,len(text)):
        if text[i]=='{': depth+=1
        elif text[i]=='}':
            depth-=1
            if depth==0: end=i+1; break
    body=text[brace+1:end-1]
    oldprefix='''
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();'''
    if oldprefix not in body: raise RuntimeError('write prefix missing '+signature)
    body=body.replace(oldprefix,'',1)
    indented='\n'.join('    '+line if line.strip() else line for line in body.strip('\n').split('\n'))
    newbody='''{
        await SqliteWriteCoordinator.ExecuteAsync(_databasePath, async connection =>
        {
'''+indented+'''
        });
    }'''
    return text[:brace]+newbody+text[end:]
text=wrap_method(text,'public async Task UpsertProfileAsync(CommissioningProfile profile)')
text=wrap_method(text,'public async Task UpsertCheckAsync(CommissioningCheckRecord record)')
write(rel,text)

print('R001.25 all SQLite writers unified')
