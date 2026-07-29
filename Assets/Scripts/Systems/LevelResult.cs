using System.Collections.Generic;

/// <summary>
/// Bir level başarıyla bitince, patika (ana menü) ekranında gösterilecek ödül özetini taşır (sahneler arası
/// static). GameManager.TriggerSuccess doldurur; MainMenuController açılışta okuyup "ödül flash"ını gösterir,
/// sonra Clear() ile temizler. (İleride "Next Level → reklam → patika" akışında reklam sonrası da geçerli.)
/// </summary>
public static class LevelResult
{
    public static bool Pending;
    public static int World, Level, Stars;
    public static int Delta;   // bu oyunda TOPLAM yıldıza net eklenen (yeni en iyi - eski en iyi)
    public static List<string> Gifts = new List<string>();
    // Yıldız akışı SUCCESS ekranında oynandıysa true → MainMenu reward-flash'ı atlar, toplam rozeti tam gösterir.
    public static bool StarsAnimated;

    public static void Set(int world, int level, int stars, int delta, List<string> gifts)
    {
        Pending = true; World = world; Level = level; Stars = stars; Delta = delta;
        Gifts = gifts ?? new List<string>();
        StarsAnimated = false;
    }

    public static void Clear()
    {
        Pending = false;
        Gifts = new List<string>();
        StarsAnimated = false;
    }
}
