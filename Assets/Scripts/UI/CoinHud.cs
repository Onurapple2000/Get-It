using UnityEngine;
using TMPro;

/// <summary>
/// Bir TMP_Text'i coin bakiyesine bağlar: aktifken PlayerProfile.OnCoinsChanged'e abone olur, harcama/kazanç
/// olunca metni otomatik günceller. Coin rozeti / store / oyun-içi HUD hepsi bununla canlı kalır.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class CoinHud : MonoBehaviour
{
    TMP_Text label;
    void Awake() { label = GetComponent<TMP_Text>(); }
    void OnEnable() { PlayerProfile.OnCoinsChanged += Refresh; Refresh(); }
    void OnDisable() { PlayerProfile.OnCoinsChanged -= Refresh; }
    void Refresh() { if (label != null) label.text = PlayerProfile.Coins.ToString(); }
}
