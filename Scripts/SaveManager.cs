using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Gestionnaire de sauvegardes SQLite.
/// Les fichiers sont stockés dans user://saves, en dehors du dossier du projet.
/// Le schéma contient les métadonnées et une table clé/valeur prête à accueillir l'état du jeu.
/// </summary>
public static class SaveManager
{
    private const string SaveDirectory = "user://saves";
    private const int SqliteOk = 0;
    private const int SqliteRow = 100;
    private const int SqliteDone = 101;

    private static string _currentSavePath = string.Empty;

    public static string CurrentSaveId { get; private set; } = string.Empty;
    public static string CurrentSaveName { get; private set; } = string.Empty;
    public static string LastError { get; private set; } = string.Empty;
    public static bool HasCurrentSave => !string.IsNullOrEmpty(_currentSavePath);

    public sealed class SaveInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string CreatedAtUtc { get; }
        public string LastPlayedAtUtc { get; }
        public string FilePath { get; }

        public SaveInfo(string id, string name, string createdAtUtc, string lastPlayedAtUtc, string filePath)
        {
            Id = id;
            Name = name;
            CreatedAtUtc = createdAtUtc;
            LastPlayedAtUtc = lastPlayedAtUtc;
            FilePath = filePath;
        }
    }

    // SQLite est fourni par Windows via winsqlite3.dll : aucune dépendance NuGet n'est nécessaire.
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_open")]
    private static extern int SqliteOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string filename, out IntPtr database);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_close")]
    private static extern int SqliteClose(IntPtr database);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_exec")]
    private static extern int SqliteExec(
        IntPtr database,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string sql,
        IntPtr callback,
        IntPtr callbackArgument,
        out IntPtr errorMessage);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_errmsg")]
    private static extern IntPtr SqliteErrorMessage(IntPtr database);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_free")]
    private static extern void SqliteFree(IntPtr memory);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_prepare_v2")]
    private static extern int SqlitePrepare(
        IntPtr database,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string sql,
        int byteCount,
        out IntPtr statement,
        IntPtr tail);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_step")]
    private static extern int SqliteStep(IntPtr statement);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_text")]
    private static extern IntPtr SqliteColumnText(IntPtr statement, int column);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_finalize")]
    private static extern int SqliteFinalize(IntPtr statement);

    /// <summary>Crée une nouvelle sauvegarde SQLite et la définit comme sauvegarde active.</summary>
    public static SaveInfo CreateSave(string name = null)
    {
        LastError = string.Empty;

        if (!EnsureSaveDirectory())
            return null;

        string id = Guid.NewGuid().ToString("N");
        string fileName = "partie_" + id + ".db";
        string filePath = SaveDirectory + "/" + fileName;
        string saveName = string.IsNullOrWhiteSpace(name)
            ? "Partie du " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("fr-FR"))
            : name.Trim();
        string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        IntPtr database = IntPtr.Zero;
        try
        {
            database = OpenDatabase(filePath);
            CreateSchema(database);
            Execute(database,
                "INSERT INTO save_metadata (id, name, created_at_utc, last_played_at_utc) VALUES (" +
                Quote(id) + ", " + Quote(saveName) + ", " + Quote(now) + ", " + Quote(now) + ");");

            SaveInfo info = ReadMetadata(database, filePath);
            if (info == null)
                throw new InvalidOperationException("Les métadonnées de la nouvelle sauvegarde sont introuvables.");

            SetCurrentSave(info);
            return info;
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            GD.PushError("SaveManager - création impossible : " + LastError);
            return null;
        }
        finally
        {
            if (database != IntPtr.Zero)
                SqliteClose(database);
        }
    }

    /// <summary>Retourne les sauvegardes valides, de la plus récemment utilisée à la plus ancienne.</summary>
    public static List<SaveInfo> ListSaves()
    {
        List<SaveInfo> saves = new List<SaveInfo>();
        LastError = string.Empty;

        if (!EnsureSaveDirectory())
            return saves;

        DirAccess directory = DirAccess.Open(SaveDirectory);
        if (directory == null)
        {
            LastError = "Impossible d'ouvrir le dossier des sauvegardes.";
            return saves;
        }

        directory.ListDirBegin();
        string fileName = directory.GetNext();

        while (!string.IsNullOrEmpty(fileName))
        {
            if (!directory.CurrentIsDir()
                && fileName.StartsWith("partie_", StringComparison.OrdinalIgnoreCase)
                && fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                string filePath = SaveDirectory + "/" + fileName;
                IntPtr database = IntPtr.Zero;

                try
                {
                    database = OpenDatabase(filePath);
                    SaveInfo info = ReadMetadata(database, filePath);
                    if (info != null)
                        saves.Add(info);
                }
                catch (Exception exception)
                {
                    GD.PushWarning("SaveManager - sauvegarde ignorée (" + fileName + ") : " + exception.Message);
                }
                finally
                {
                    if (database != IntPtr.Zero)
                        SqliteClose(database);
                }
            }

            fileName = directory.GetNext();
        }

        directory.ListDirEnd();
        saves.Sort((left, right) => string.Compare(
            right.LastPlayedAtUtc, left.LastPlayedAtUtc, StringComparison.Ordinal));
        return saves;
    }

    public static SaveInfo GetLatestSave()
    {
        List<SaveInfo> saves = ListSaves();
        return saves.Count > 0 ? saves[0] : null;
    }

    /// <summary>Ouvre logiquement une sauvegarde et actualise sa date de dernière utilisation.</summary>
    public static bool OpenSave(string id)
    {
        LastError = string.Empty;
        SaveInfo target = ListSaves().Find(save => save.Id == id);

        if (target == null)
        {
            LastError = "La sauvegarde sélectionnée n'existe plus ou est invalide.";
            return false;
        }

        IntPtr database = IntPtr.Zero;
        try
        {
            database = OpenDatabase(target.FilePath);
            string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            Execute(database, "UPDATE save_metadata SET last_played_at_utc = " + Quote(now) +
                " WHERE id = " + Quote(target.Id) + ";");

            SaveInfo opened = ReadMetadata(database, target.FilePath);
            if (opened == null)
                throw new InvalidOperationException("Impossible de relire les métadonnées de la sauvegarde.");

            SetCurrentSave(opened);
            return true;
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            GD.PushError("SaveManager - ouverture impossible : " + LastError);
            return false;
        }
        finally
        {
            if (database != IntPtr.Zero)
                SqliteClose(database);
        }
    }

    /// <summary>Supprime le fichier .db associé à l'identifiant donné.</summary>
    public static bool DeleteSave(string id)
    {
        LastError = string.Empty;
        SaveInfo target = ListSaves().Find(save => save.Id == id);

        if (target == null)
        {
            LastError = "La sauvegarde sélectionnée n'existe plus.";
            return false;
        }

        DirAccess directory = DirAccess.Open(SaveDirectory);
        if (directory == null)
        {
            LastError = "Impossible d'ouvrir le dossier des sauvegardes.";
            return false;
        }

        Error result = directory.Remove(Path.GetFileName(target.FilePath));
        if (result != Error.Ok)
        {
            LastError = "La suppression du fichier a échoué : " + result;
            return false;
        }

        if (CurrentSaveId == id)
            ClearCurrentSave();

        return true;
    }

    /// <summary>Enregistre une valeur de jeu pour la sauvegarde active (base prête pour les futures données).</summary>
    public static bool SetValue(string key, string value)
    {
        LastError = string.Empty;
        if (!HasCurrentSave)
        {
            LastError = "Aucune sauvegarde active.";
            return false;
        }

        IntPtr database = IntPtr.Zero;
        try
        {
            database = OpenDatabase(_currentSavePath);
            Execute(database, "CREATE TABLE IF NOT EXISTS game_state (key TEXT PRIMARY KEY, value TEXT NOT NULL);");
            Execute(database, "INSERT OR REPLACE INTO game_state (key, value) VALUES (" +
                Quote(key ?? string.Empty) + ", " + Quote(value ?? string.Empty) + ");");
            return true;
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            GD.PushError("SaveManager - écriture impossible : " + LastError);
            return false;
        }
        finally
        {
            if (database != IntPtr.Zero)
                SqliteClose(database);
        }
    }

    /// <summary>Lit une valeur de jeu de la sauvegarde active.</summary>
    public static string GetValue(string key, string defaultValue = "")
    {
        LastError = string.Empty;
        if (!HasCurrentSave)
        {
            LastError = "Aucune sauvegarde active.";
            return defaultValue;
        }

        IntPtr database = IntPtr.Zero;
        IntPtr statement = IntPtr.Zero;
        try
        {
            database = OpenDatabase(_currentSavePath);
            Execute(database, "CREATE TABLE IF NOT EXISTS game_state (key TEXT PRIMARY KEY, value TEXT NOT NULL);");

            int result = SqlitePrepare(database,
                "SELECT value FROM game_state WHERE key = " + Quote(key ?? string.Empty) + " LIMIT 1;",
                -1, out statement, IntPtr.Zero);

            if (result != SqliteOk)
                throw new InvalidOperationException(GetDatabaseError(database));

            int step = SqliteStep(statement);
            if (step == SqliteRow)
            {
                IntPtr valuePointer = SqliteColumnText(statement, 0);
                return valuePointer == IntPtr.Zero ? defaultValue : Marshal.PtrToStringUTF8(valuePointer);
            }

            if (step != SqliteDone)
                throw new InvalidOperationException(GetDatabaseError(database));

            return defaultValue;
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            GD.PushError("SaveManager - lecture impossible : " + LastError);
            return defaultValue;
        }
        finally
        {
            if (statement != IntPtr.Zero)
                SqliteFinalize(statement);
            if (database != IntPtr.Zero)
                SqliteClose(database);
        }
    }

    private static bool EnsureSaveDirectory()
    {
        if (DirAccess.Open(SaveDirectory) != null)
            return true;

        DirAccess root = DirAccess.Open("user://");
        if (root == null)
        {
            LastError = "Impossible d'accéder au dossier utilisateur du jeu.";
            GD.PushError("SaveManager - " + LastError);
            return false;
        }

        Error result = root.MakeDir("saves");
        if (result != Error.Ok && result != Error.AlreadyExists)
        {
            LastError = "Impossible de créer le dossier des sauvegardes : " + result;
            GD.PushError("SaveManager - " + LastError);
            return false;
        }

        if (DirAccess.Open(SaveDirectory) == null)
        {
            LastError = "Le dossier des sauvegardes reste inaccessible.";
            return false;
        }

        return true;
    }

    private static IntPtr OpenDatabase(string userPath)
    {
        string absolutePath = ProjectSettings.GlobalizePath(userPath);
        int result = SqliteOpen(absolutePath, out IntPtr database);

        if (result != SqliteOk)
        {
            string message = database == IntPtr.Zero ? "SQLite n'a pas pu ouvrir le fichier." : GetDatabaseError(database);
            if (database != IntPtr.Zero)
                SqliteClose(database);
            throw new InvalidOperationException(message + " (code SQLite " + result + ").");
        }

        return database;
    }

    private static void CreateSchema(IntPtr database)
    {
        Execute(database,
            "PRAGMA user_version = 1;" +
            "CREATE TABLE IF NOT EXISTS save_metadata (" +
            "id TEXT PRIMARY KEY, name TEXT NOT NULL, created_at_utc TEXT NOT NULL, last_played_at_utc TEXT NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS game_state (key TEXT PRIMARY KEY, value TEXT NOT NULL);");
    }

    private static void Execute(IntPtr database, string sql)
    {
        int result = SqliteExec(database, sql, IntPtr.Zero, IntPtr.Zero, out IntPtr errorPointer);
        if (result == SqliteOk)
            return;

        string message = errorPointer == IntPtr.Zero
            ? GetDatabaseError(database)
            : Marshal.PtrToStringUTF8(errorPointer);

        if (errorPointer != IntPtr.Zero)
            SqliteFree(errorPointer);

        throw new InvalidOperationException(message + " (code SQLite " + result + ").");
    }

    private static SaveInfo ReadMetadata(IntPtr database, string filePath)
    {
        IntPtr statement = IntPtr.Zero;
        try
        {
            int result = SqlitePrepare(database,
                "SELECT id, name, created_at_utc, last_played_at_utc FROM save_metadata LIMIT 1;",
                -1, out statement, IntPtr.Zero);

            if (result != SqliteOk)
                throw new InvalidOperationException(GetDatabaseError(database));

            int step = SqliteStep(statement);
            if (step == SqliteDone)
                return null;
            if (step != SqliteRow)
                throw new InvalidOperationException(GetDatabaseError(database));

            return new SaveInfo(
                ReadColumn(statement, 0),
                ReadColumn(statement, 1),
                ReadColumn(statement, 2),
                ReadColumn(statement, 3),
                filePath);
        }
        finally
        {
            if (statement != IntPtr.Zero)
                SqliteFinalize(statement);
        }
    }

    private static string ReadColumn(IntPtr statement, int column)
    {
        IntPtr value = SqliteColumnText(statement, column);
        return value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(value);
    }

    private static string GetDatabaseError(IntPtr database)
    {
        IntPtr message = SqliteErrorMessage(database);
        return message == IntPtr.Zero ? "Erreur SQLite inconnue." : Marshal.PtrToStringUTF8(message);
    }

    private static string Quote(string value)
    {
        return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
    }

    private static void SetCurrentSave(SaveInfo save)
    {
        CurrentSaveId = save.Id;
        CurrentSaveName = save.Name;
        _currentSavePath = save.FilePath;
    }

    private static void ClearCurrentSave()
    {
        CurrentSaveId = string.Empty;
        CurrentSaveName = string.Empty;
        _currentSavePath = string.Empty;
    }
}
