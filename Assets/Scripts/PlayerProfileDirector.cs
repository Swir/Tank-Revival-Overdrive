using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TankRevival
{
    public sealed class PlayerProfileDirector : MonoBehaviour
    {
        [Serializable]
        private sealed class ProfilePayload
        {
            public int schemaVersion = 1;
            public int furthestRound = 1;
            public int lastRound = 1;
            public int runsStarted;
            public int totalRoundTransitions;
            public int legacyHighScore;
            public int recoveryCount;
            public string lastSeenVersion = "unknown";
            public string lastWriteUtc = string.Empty;
        }

        [Serializable]
        private sealed class ProfileEnvelope
        {
            public ProfilePayload payload = new ProfilePayload();
            public string checksum = string.Empty;
        }

        private const string ProfileFileName = "player-profile.json";
        private const string BackupFileName = "player-profile.backup.json";
        private const string TempFileName = "player-profile.tmp";
        private const string LegacyHighScoreKey = "TankRevival.HighScore";

        private static PlayerProfileDirector _instance;
        private TankGame _game;
        private ProfilePayload _profile;
        private int _observedRound = -1;
        private bool _observedPlaying;
        private float _nextAutosaveAt;
        private bool _dirty;

        public static PlayerProfileDirector Instance => _instance;
        public int FurthestRound => _profile != null ? _profile.furthestRound : 1;
        public int RunsStarted => _profile != null ? _profile.runsStarted : 0;
        public int RecoveryCount => _profile != null ? _profile.recoveryCount : 0;
        public bool LoadedFromRecovery { get; private set; }

        private string ProfilePath => Path.Combine(Application.persistentDataPath, ProfileFileName);
        private string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);
        private string TempPath => Path.Combine(Application.persistentDataPath, TempFileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindAnyObjectByType<PlayerProfileDirector>() != null) return;
            GameObject go = new GameObject("PlayerProfileDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerProfileDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadOrRecover();
            _nextAutosaveAt = Time.unscaledTime + 10f;
        }

        private void Update()
        {
            if (_game == null)
                _game = FindAnyObjectByType<TankGame>();

            if (_game == null)
                return;

            bool playing = _game.IsPlaying;
            int round = Mathf.Max(1, _game.CurrentRound);

            if (playing && !_observedPlaying)
            {
                _profile.runsStarted++;
                MarkDirty();
            }

            if (_observedRound > 0 && round != _observedRound)
            {
                if (round > _observedRound)
                    _profile.totalRoundTransitions += round - _observedRound;

                _profile.lastRound = round;
                _profile.furthestRound = Mathf.Max(_profile.furthestRound, round);
                MarkDirty();
                SaveNow();
            }
            else if (_observedRound < 0)
            {
                _profile.lastRound = round;
                _profile.furthestRound = Mathf.Max(_profile.furthestRound, round);
                MarkDirty();
            }

            _observedPlaying = playing;
            _observedRound = round;

            if (_dirty && Time.unscaledTime >= _nextAutosaveAt)
                SaveNow();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) SaveNow();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) SaveNow();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void LoadOrRecover()
        {
            LoadedFromRecovery = false;

            ProfilePayload primary;
            if (TryRead(ProfilePath, out primary))
            {
                _profile = primary;
                MergeLegacyState();
                return;
            }

            ProfilePayload backup;
            if (TryRead(BackupPath, out backup))
            {
                _profile = backup;
                _profile.recoveryCount++;
                LoadedFromRecovery = true;
                MergeLegacyState();
                MarkDirty();
                SaveNow();
                Debug.LogWarning("[TankRevival] Player profile recovered from backup.");
                return;
            }

            _profile = new ProfilePayload();
            MergeLegacyState();
            MarkDirty();
            SaveNow();
        }

        private void MergeLegacyState()
        {
            if (_profile == null)
                _profile = new ProfilePayload();

            _profile.schemaVersion = Mathf.Max(1, _profile.schemaVersion);
            _profile.furthestRound = Mathf.Max(1, _profile.furthestRound);
            _profile.lastRound = Mathf.Max(1, _profile.lastRound);
            _profile.legacyHighScore = Mathf.Max(_profile.legacyHighScore, PlayerPrefs.GetInt(LegacyHighScoreKey, 0));
            _profile.lastSeenVersion = Application.version;
        }

        public void SaveNow()
        {
            if (_profile == null || !_dirty)
                return;

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                _profile.lastSeenVersion = Application.version;
                _profile.lastWriteUtc = DateTime.UtcNow.ToString("O");

                string payloadJson = JsonUtility.ToJson(_profile, false);
                ProfileEnvelope envelope = new ProfileEnvelope
                {
                    payload = _profile,
                    checksum = ComputeChecksum(payloadJson)
                };

                string envelopeJson = JsonUtility.ToJson(envelope, true);
                File.WriteAllText(TempPath, envelopeJson, Encoding.UTF8);

                ProfilePayload verification;
                if (!TryRead(TempPath, out verification))
                    throw new InvalidDataException("Temporary profile verification failed.");

                if (File.Exists(ProfilePath))
                {
                    try
                    {
                        File.Replace(TempPath, ProfilePath, BackupPath, true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(ProfilePath, BackupPath, true);
                        File.Copy(TempPath, ProfilePath, true);
                        File.Delete(TempPath);
                    }
                    catch (IOException)
                    {
                        File.Copy(ProfilePath, BackupPath, true);
                        File.Copy(TempPath, ProfilePath, true);
                        File.Delete(TempPath);
                    }
                }
                else
                {
                    File.Move(TempPath, ProfilePath);
                    File.Copy(ProfilePath, BackupPath, true);
                }

                _dirty = false;
                _nextAutosaveAt = Time.unscaledTime + 10f;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TankRevival] Player profile save failed safely: " + ex.Message);
                _dirty = true;
                _nextAutosaveAt = Time.unscaledTime + 5f;
                TryDeleteTemp();
            }
        }

        private bool TryRead(string path, out ProfilePayload payload)
        {
            payload = null;
            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return false;

                ProfileEnvelope envelope = JsonUtility.FromJson<ProfileEnvelope>(json);
                if (envelope == null || envelope.payload == null || string.IsNullOrEmpty(envelope.checksum))
                    return false;

                string payloadJson = JsonUtility.ToJson(envelope.payload, false);
                string expected = ComputeChecksum(payloadJson);
                if (!SlowEquals(expected, envelope.checksum))
                    return false;

                if (envelope.payload.schemaVersion < 1)
                    return false;

                payload = envelope.payload;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string ComputeChecksum(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static bool SlowEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private void TryDeleteTemp()
        {
            try
            {
                if (File.Exists(TempPath)) File.Delete(TempPath);
            }
            catch (Exception)
            {
                // Best-effort cleanup only. Never make profile recovery a gameplay blocker.
            }
        }
    }
}
