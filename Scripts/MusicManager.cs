using Godot;

/// <summary>
/// Gère la musique de fond et le volume général.
/// Le lecteur est ajouté à la racine du SceneTree afin de survivre aux changements de scène.
/// </summary>
public static class MusicManager
{
    private const string MusicPath = "res://musics/Music.mp3";
    private const string OptionsPath = "user://options.cfg";
    private const string AudioSection = "audio";
    private const string MasterVolumeKey = "master_volume";

    private static AudioStreamPlayer _player;

    /// <summary>
    /// Initialise la musique une seule fois. À appeler depuis le menu de démarrage.
    /// </summary>
    public static void Initialize(SceneTree tree)
    {
        if (_player != null && GodotObject.IsInstanceValid(_player))
            return;

        _player = new AudioStreamPlayer
        {
            Name = "MusicManagerPlayer",
            Bus = "Master"
        };

        tree.Root.AddChild(_player);
        ApplySavedVolume();

        if (!ResourceLoader.Exists(MusicPath))
        {
            GD.PushWarning($"MusicManager : fichier musical introuvable : {MusicPath}");
            return;
        }

        AudioStreamMP3 music = ResourceLoader.Load<AudioStreamMP3>(MusicPath);
        if (music == null)
        {
            GD.PushWarning($"MusicManager : impossible de charger : {MusicPath}");
            return;
        }

        music.Loop = true;
        _player.Stream = music;
        _player.Play();
    }

    /// <summary>
    /// Définit le volume général entre 0 et 1 et sauvegarde le réglage.
    /// </summary>
    public static void SetMasterVolume(float linearVolume)
    {
        linearVolume = Mathf.Clamp(linearVolume, 0.0f, 1.0f);

        int masterBus = AudioServer.GetBusIndex("Master");
        if (masterBus >= 0)
        {
            float decibels = linearVolume <= 0.0f
                ? -80.0f
                : Mathf.LinearToDb(linearVolume);

            AudioServer.SetBusVolumeDb(masterBus, decibels);
        }

        var config = new ConfigFile();
        config.Load(OptionsPath);
        config.SetValue(AudioSection, MasterVolumeKey, linearVolume);
        Error saveError = config.Save(OptionsPath);

        if (saveError != Error.Ok)
            GD.PushWarning($"MusicManager : impossible de sauvegarder le volume ({saveError}).");
    }

    /// <summary>
    /// Renvoie le volume général sauvegardé, ou 1 si aucun réglage n'existe.
    /// </summary>
    public static float GetSavedMasterVolume()
    {
        var config = new ConfigFile();
        if (config.Load(OptionsPath) != Error.Ok)
            return 1.0f;

        Variant value = config.GetValue(AudioSection, MasterVolumeKey, 1.0f);
        return Mathf.Clamp((float)value, 0.0f, 1.0f);
    }

    private static void ApplySavedVolume()
    {
        float volume = GetSavedMasterVolume();
        int masterBus = AudioServer.GetBusIndex("Master");

        if (masterBus < 0)
            return;

        float decibels = volume <= 0.0f ? -80.0f : Mathf.LinearToDb(volume);
        AudioServer.SetBusVolumeDb(masterBus, decibels);
    }
}
