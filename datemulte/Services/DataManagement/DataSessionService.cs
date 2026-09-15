using System.Security.Cryptography;
using Datemulte_2.Data;
using Datemulte_2.Models;
using Datemulte_2.Models.DataManagement;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Datemulte_2.Services.DataManagement;

public class DataSessionService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
    private readonly HybridStateManager _stateManager;
    private readonly CsvService _csvService;
    private readonly ILogger<DataSessionService> _logger;
    private readonly string _filesStoragePath;

    public DataSessionService(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        HybridStateManager stateManager,
        CsvService csvService,
        ILogger<DataSessionService> logger,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _stateManager = stateManager;
        _csvService = csvService;
        _logger = logger;

        // Use configuration or fall back to a cross-platform default path
        var configuredPath = configuration["FileStorage:FileSystemBasePath"];
        if (!string.IsNullOrEmpty(configuredPath))
        {
            _filesStoragePath = configuredPath;
        }
        else
        {
            // Cross-platform default: use the application's data directory
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _filesStoragePath = Path.Combine(appDataPath, "Datemulte", "DataFiles");
        }

        Directory.CreateDirectory(_filesStoragePath);
    }

    public async Task<(Guid sessionId, List<GenericCsvRow> data)> ProcessFileUploadAsync(IBrowserFile file, Guid userId, string? selectedSheet = null)
    {
        _logger.LogInformation("Processing file upload: {FileName} ({Size} bytes)", file.Name, file.Size);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        try
        {
            var sourceFile = await SaveFileToStorageAsync(file, userId, dbContext);

            List<GenericCsvRow> data;
            List<string> columns;
            List<string> numericColumns;
            string? timeColumn;
            Dictionary<string, string> columnGroups;
            List<string> sensorGroups;
            Dictionary<string, string> columnUnits;

            var extension = Path.GetExtension(file.Name).ToLower();

            if(extension == ".xlsx" || extension == ".xls")
            {
                using var stream = File.OpenRead(ResolveStoragePath(sourceFile.StoragePath));

                if(selectedSheet != null)
                {
                    (data, columns, numericColumns, timeColumn, columnGroups, sensorGroups, columnUnits) = await _csvService.ParseExcelSheetAsync(stream, selectedSheet);

                    sourceFile.SheetName = selectedSheet;
                    sourceFile.IsGroupedFormat = _csvService.IsGroupedFormat;
                }
                else
                {
                    throw new InvalidOperationException("Excel file requires selection");
                }
            }
            else
            {
                using var stream = File.OpenRead(ResolveStoragePath(sourceFile.StoragePath));

                (data, columns, numericColumns, timeColumn, columnGroups, sensorGroups, columnUnits) = await _csvService.ParseGenericCsvAsync(stream);

                sourceFile.IsGroupedFormat = _csvService.IsGroupedFormat;
            }

            sourceFile.ProcessingStatus = "Completed";
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Parsed {RowCount} rows, {columnCount} columns", data.Count, columns.Count);

            var session = new DataSession
            {
                SessionId = Guid.NewGuid(),
                SessionName = Path.GetFileNameWithoutExtension(file.Name),
                UserId = userId,
                SourceFileId = sourceFile.FileId,
                SourceFileName = file.Name,
                SourceFileSize = file.Size,
                SourceFileHash = sourceFile.FileHash,
                Columns = columns,
                NumericColumns = numericColumns,
                TimeColumn = timeColumn,
                ColumnUnits = columnUnits,
                ColumnToGroupMapping = columnGroups,
                SensorGroups = sensorGroups
            };

            await dbContext.DataSessions.AddAsync(session);
            sourceFile.SessionId = session.SessionId;
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Created session {SessionId}", session.SessionId);

            await _stateManager.CreateInitialSnapshotAsync(session.SessionId, data);

            _logger.LogInformation("File processing complete for session {SessionId}", session.SessionId);

            return (session.SessionId, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload");
            throw;
        }
    }

    /// <summary>
    /// Creates a session from already-loaded data (for multiplayer mode when data is already in memory)
    /// </summary>
    public async Task<Guid> CreateSessionFromDataAsync(
        List<GenericCsvRow> data,
        List<string> columns,
        List<string> numericColumns,
        string? timeColumn,
        Dictionary<string, string> columnUnits,
        Dictionary<string, string> columnGroups,
        List<string> sensorGroups,
        string sessionName,
        string sourceFileName,
        Guid userId)
    {
        _logger.LogInformation("Creating session from loaded data: {SessionName} ({RowCount} rows)", sessionName, data.Count);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        try
        {
            var session = new DataSession
            {
                SessionId = Guid.NewGuid(),
                SessionName = sessionName,
                UserId = userId,
                SourceFileName = sourceFileName,
                SourceFileSize = 0,
                Columns = columns,
                NumericColumns = numericColumns,
                TimeColumn = timeColumn,
                ColumnUnits = columnUnits,
                ColumnToGroupMapping = columnGroups,
                SensorGroups = sensorGroups
            };

            await dbContext.DataSessions.AddAsync(session);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Created session {SessionId}", session.SessionId);

            await _stateManager.CreateInitialSnapshotAsync(session.SessionId, data);

            _logger.LogInformation("Session creation complete for {SessionId}", session.SessionId);

            return session.SessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session from data");
            throw;
        }
    }

    public async Task<(DataSession session, List<GenericCsvRow> data)> LoadSessionAsync(Guid sessionId, Guid? userId = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var session = await dbContext.DataSessions.FindAsync(sessionId);

        if(session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        if(userId.HasValue && session.UserId != userId.Value)
        {
            _logger.LogWarning("User {UserId} attempted to access session {SessionId} owned by {OwnerId}",
                userId.Value, sessionId, session.UserId);
            throw new UnauthorizedAccessException("You do not have permission to access this session");
        }

        var data = await _stateManager.LoadCurrentStateAsync(sessionId);

        return (session, data);
    }

    public async Task SaveCellEditAsync(Guid sessionId, int rowIndex, string columnName, string oldValue, string newValue, Guid? userId = null)
    {
        await _stateManager.SaveEditAsync(sessionId, new DataChangeEvent
        {
            EventType = DataEventType.CellUpdate,
            RowIndex = rowIndex,
            ColumnName = columnName,
            OldValue = oldValue,
            NewValue = newValue,
            UserId = userId
        });
    }

    public async Task<SessionStatistics> GetSessionStatisticsAsync(Guid sessionId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var session = await dbContext.DataSessions.FindAsync(sessionId);

        if(session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        var snapshotCount = await dbContext.DataSnapshots.CountAsync(s => s.SessionId == sessionId);

        var eventCount =  await dbContext.DataChangeEvents.CountAsync(e => e.SessionId == sessionId);

        var latestSnapshot = await dbContext.DataSnapshots.Where(s => s.SessionId == sessionId).OrderByDescending(s => s.VersionNumber).FirstOrDefaultAsync();

        return new SessionStatistics
        {
            SessionId = sessionId,
            SessionName = session.SessionName,
            CreatedAt = session.CreatedAt,
            LastModifiedAt = session.LastModifiedAt,
            CurrentVersion = session.CurrentVersion,
            TotalSnapshots = snapshotCount,
            TotalEdits = eventCount,
            CurrentRowCount = latestSnapshot?.RowCount ?? 0,
            FileSizeBytes = session.SourceFileSize
        };
    }

    public async Task<List<DataSession>> GetUserSessionsAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.DataSessions
            .Where(s => s.UserId == userId).OrderByDescending(s => s.LastModifiedAt).ToListAsync();
    }

    public async Task<List<DataSession>> GetAllSessionsAsync(Guid? userId = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var query = dbContext.DataSessions.AsQueryable();

        if(userId.HasValue)
        {
            query = query.Where(s => s.UserId == userId.Value);
        }

        return await query.OrderByDescending(s => s.LastModifiedAt).ToListAsync();
    }

    public async Task DeleteSessionAsync(Guid sessionId, Guid? userId = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            var session = await dbContext.DataSessions.FindAsync(sessionId);
            if (session == null) return;

            if(userId.HasValue && session.UserId != userId.Value)
            {
                _logger.LogWarning("User {UserId} attempted to delete session {SessionId} owned by {OwnerId}",
                    userId.Value, sessionId, session.UserId);
                throw new UnauthorizedAccessException("You do not have permission to delete this session");
            }

            if (session.SourceFileId.HasValue)
            {
                var sourceFile = await dbContext.SourceFiles.FindAsync(session.SourceFileId.Value);
                if (sourceFile != null)
                {
                    if (File.Exists(ResolveStoragePath(sourceFile.StoragePath)))
                    {
                        File.Delete(ResolveStoragePath(sourceFile.StoragePath));
                    }

                    dbContext.SourceFiles.Remove(sourceFile);
                }
            }

            var uiState = await dbContext.SessionUIStates.FindAsync(sessionId);
            if (uiState != null)
            {
                dbContext.SessionUIStates.Remove(uiState);
            }

            dbContext.DataSessions.Remove(session);

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Deleted session {SessionId} and associated resources", sessionId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error deleting session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task UpdateSessionNameAsync(Guid sessionId, string newName, Guid? userId = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var session = await dbContext.DataSessions.FindAsync(sessionId);
        if(session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        if(userId.HasValue && session.UserId != userId.Value)
        {
            _logger.LogWarning("User {UserId} attempted to update session {SessionId} owned by {OwnerId}",
                userId.Value, sessionId, session.UserId);
            throw new UnauthorizedAccessException("You do not have permission to update this session");
        }

        session.SessionName = newName;
        session.LastModifiedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated session {SessionId} name to {NewName}", sessionId, newName);
    }

    public async Task SaveSessionUIStateAsync(SessionUIState uiState, Guid? userId = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        if(userId.HasValue)
        {
            var session = await dbContext.DataSessions.FindAsync(uiState.SessionId);
            if(session != null && session.UserId != userId.Value)
            {
                _logger.LogWarning("User {UserId} attempted to save UI state for session {SessionId} owned by {OwnerId}",
                    userId.Value, uiState.SessionId, session.UserId);
                throw new UnauthorizedAccessException("You do not have permission to modify this session");
            }
        }

        var existing = await dbContext.SessionUIStates.FindAsync(uiState.SessionId);

        if (existing != null)
        {
            dbContext.Entry(existing).CurrentValues.SetValues(uiState);
            existing.LastUpdatedAt = DateTime.UtcNow;
        }
        else
        {
            uiState.LastUpdatedAt = DateTime.UtcNow;
            await dbContext.SessionUIStates.AddAsync(uiState);
        }

        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Saved UI state for session {SessionId}", uiState.SessionId);
    }

    public async Task<SessionUIState?> LoadSessionUIStateAsync(Guid sessionId, Guid? userId = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        if(userId.HasValue)
        {
            var session = await dbContext.DataSessions.FindAsync(sessionId);
            if(session != null && session.UserId != userId.Value)
            {
                _logger.LogWarning("User {UserId} attempted to load UI state for session {SessionId} owned by {OwnerId}",
                    userId.Value, sessionId, session.UserId);
                throw new UnauthorizedAccessException("You do not have permission to access this session");
            }
        }

        var uiState = await dbContext.SessionUIStates.FindAsync(sessionId);

        if (uiState != null)
        {
            _logger.LogInformation("Loaded UI state for session {SessionId}", sessionId);
        }

        return uiState;
    }

    // Relative names remain valid after the complete data folder is moved.
    private string ResolveStoragePath(string storedName)
    {
        if (Path.GetFileName(storedName) != storedName)
            throw new InvalidOperationException("Invalid local file name.");
        return Path.Combine(_filesStoragePath, storedName);
    }

    private async Task<SourceFile> SaveFileToStorageAsync(IBrowserFile file, Guid userId, DataManagementDbContext dbContext)
    {
        var fileId = Guid.NewGuid();
        var fileName = $"{fileId}_{Path.GetFileName(file.Name)}";
        var filePath = Path.Combine(_filesStoragePath, fileName);

        byte[] hash;

        using var sha256 = SHA256.Create();
        await using (var fileStream = File.Create(filePath))
        await using (var cryptoStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write))
        {
            await using var uploadStream = file.OpenReadStream(maxAllowedSize: 1024 * 1024 * 1024);
            await uploadStream.CopyToAsync(cryptoStream);
        }

        hash = sha256.Hash!;

        var sourceFile = new SourceFile
        {
            FileId = fileId,
            FileName = file.Name,
            FileSizeBytes = file.Size,
            ContentType = file.ContentType,
            StorageProvider = "FileSystem",
            StoragePath = fileName,
            FileHash = Convert.ToHexString(hash).ToLowerInvariant(),
            UploadedBy = userId,
            ProcessingStatus = "Processing"
        };

        await dbContext.SourceFiles.AddAsync(sourceFile);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Saved file to {Path}", filePath);

        return sourceFile;
    }

    public async Task<DataSession?> FindLatestSessionByFileNameAsync(string fileName, Guid? userId = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var query = dbContext.DataSessions.Where(s => s.SourceFileName == fileName);

        if (userId.HasValue)
        {
            query = query.Where(s => s.UserId == userId.Value);
        }

        return await query.OrderByDescending(s => s.LastModifiedAt).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Append data rows to an existing session
    /// </summary>
    public async Task AppendDataAsync(Guid sessionId, List<Dictionary<string, object>> rows, Guid? userId = null)
    {
        var (session, existingData) = await LoadSessionAsync(sessionId, userId);

        // Convert object dictionary to string dictionary
        var newRows = rows.Select(r => new GenericCsvRow
        {
            Data = r.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "")
        }).ToList();

        existingData.AddRange(newRows);

        // Serialize and save using CreateSnapshotAsync
        await CreateSnapshotAsync(sessionId, existingData, "Added rows via API");
    }

    /// <summary>
    /// Update a specific row in a session
    /// </summary>
    public async Task UpdateRowAsync(Guid sessionId, int rowIndex, Dictionary<string, object> updatedRow, Guid? userId = null)
    {
        var (session, data) = await LoadSessionAsync(sessionId, userId);

        if (rowIndex < 0 || rowIndex >= data.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex), "Row index out of range");
        }

        // Convert object dictionary to string dictionary
        data[rowIndex].Data = updatedRow.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "");

        await CreateSnapshotAsync(sessionId, data, $"Updated row {rowIndex} via API");
    }

    /// <summary>
    /// Delete a specific row from a session
    /// </summary>
    public async Task DeleteRowAsync(Guid sessionId, int rowIndex, Guid? userId = null)
    {
        var (session, data) = await LoadSessionAsync(sessionId, userId);

        if (rowIndex < 0 || rowIndex >= data.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex), "Row index out of range");
        }

        data.RemoveAt(rowIndex);

        await CreateSnapshotAsync(sessionId, data, $"Deleted row {rowIndex} via API");
    }

    private async Task CreateSnapshotAsync(Guid sessionId, List<GenericCsvRow> data, string description)
    {
        await _stateManager.CreateInitialSnapshotAsync(sessionId, data);
        _logger.LogInformation("Created snapshot for session {SessionId}: {Description}", sessionId, description);
    }
}

public class SessionStatistics
{
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public int CurrentVersion { get; set; }
    public int TotalSnapshots { get; set; }
    public int TotalEdits { get; set; }
    public int CurrentRowCount { get; set; }
    public long FileSizeBytes { get; set; }
}