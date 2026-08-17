using UnityEngine;

/// <summary>
/// Oyuncu profili — kalıcı (PlayerPrefs), her açılışta hazır. İsim (varsayılan "Player"), oyun parası (coin),
/// reklamsız (No-Ads) bayrağı ve zorluk (DifficultySettings üzerinden). Güç-up adetleri PowerUpInventory'de.
/// </summary>
public static class PlayerProfile
{
    public const string DefaultName = "Player";

    const string KEY_NAME    = "PlayerName";
    const string KEY_NAMESET = "PlayerNameChosen";
    const string KEY_COINS   = "Coins";
    const string KEY_NOADS   = "NoAds";
    const string KEY_SCORE   = "TotalScore";

    /// <summary>Oyuncu ilk kez isim kaydetti mi? (İlk açılışta isim girme ekranı için.)</summary>
    public static bool NameChosen => PlayerPrefs.GetInt(KEY_NAMESET, 0) == 1;

    public static string Name
    {
        // Özel ad girilmemişse (boş veya İngilizce "Player") → dile göre YEREL varsayılan (Arapça'da لاعب vb.).
        get
        {
            var n = PlayerPrefs.GetString(KEY_NAME, "");
            if (string.IsNullOrWhiteSpace(n) || n == DefaultName) return Loc.T("defaultPlayer");
            return n;
        }
        set
        {
            string n = string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
            n = TitleCase(n);                    // her kelime: ilk harf BÜYÜK, geri kalan küçük (kullanıcı 2026-08-17)
            if (n.Length > 16) n = n.Substring(0, 16);
            // Varsayılan (herhangi dildeki "Player") girildiyse ÖZEL sayma → boş sakla (dile göre dinamik yerelleşir).
            if (n == DefaultName || n == Loc.T("defaultPlayer")) n = "";
            PlayerPrefs.SetString(KEY_NAME, n);
            PlayerPrefs.SetInt(KEY_NAMESET, 1);   // seçilmiş sayılır (tekrar sorulmaz)
            PlayerPrefs.Save();
        }
    }

    /// <summary>Girilen adı kelime-bazlı "Title Case" yapar: her kelimenin ilk harfi büyük, gerisi küçük (ör. "onUR aSLAN" → "Onur Aslan").</summary>
    static string TitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var parts = s.Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Length == 0) continue;
            parts[i] = char.ToUpper(p[0]) + (p.Length > 1 ? p.Substring(1).ToLower() : "");
        }
        return string.Join(" ", parts);
    }

    public static int Coins
    {
        get => PlayerPrefs.GetInt(KEY_COINS, 0);
        private set { PlayerPrefs.SetInt(KEY_COINS, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }
    public static void AddCoins(int n) { if (n > 0) Coins = Coins + n; }
    public static bool TrySpendCoins(int n)
    {
        if (n <= 0) return true;
        if (Coins < n) return false;
        Coins -= n; return true;
    }

    /// <summary>Ömür boyu toplam skor (her başarılı level sonunda o levelın skoru eklenir). Ana sayfada gösterilir.</summary>
    public static int TotalScore
    {
        get => PlayerPrefs.GetInt(KEY_SCORE, 0);
        private set { PlayerPrefs.SetInt(KEY_SCORE, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }
    public static void AddScore(int n) { if (n > 0) TotalScore = TotalScore + n; }

    public static bool NoAds
    {
        get => PlayerPrefs.GetInt(KEY_NOADS, 0) == 1;
        set { PlayerPrefs.SetInt(KEY_NOADS, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static Difficulty Difficulty
    {
        get => DifficultySettings.Current;
        set => DifficultySettings.Current = value;
    }
}
