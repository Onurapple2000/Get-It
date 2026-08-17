using System.Collections.Generic;
using UnityEngine;

/// <summary>Oyun dili. Sıra = tablo dizisi indexi (TR,EN,ES,DE,AR,KO,RU).</summary>
public enum Language { Turkish = 0, English = 1, Spanish = 2, German = 3, Arabic = 4, Korean = 5, Russian = 6 }

/// <summary>
/// Lokalizasyon (2026-08-17): TR + EN + ES + DE + AR + KO + RU. Dil PlayerPrefs'te kalıcı; ilk açılışta bayraklarla
/// seçtirilir, Ayarlar'dan değiştirilebilir. `Loc.T(key)` aktif dile göre metni döndürür (dizi indexi = (int)Language).
/// ⚠️ Korece/Arapça glifleri için TMP fontunda o script yoksa kutu görünür (font asset/fallback ayrı iş; Arapça RTL de).
/// </summary>
public static class Loc
{
    const string KEY = "Language", KEY_SET = "LanguageChosen";

    public static bool Chosen => PlayerPrefs.GetInt(KEY_SET, 0) == 1;

    public static Language Current
    {
        get => (Language)Mathf.Clamp(PlayerPrefs.GetInt(KEY, 0), 0, 6);
        set { PlayerPrefs.SetInt(KEY, (int)value); PlayerPrefs.SetInt(KEY_SET, 1); PlayerPrefs.Save(); }
    }

    public static bool En => Current == Language.English;

    public static string T(string key)
        => Table.TryGetValue(key, out var a) ? a[Mathf.Clamp((int)Current, 0, a.Length - 1)] : key;

    // Sıra: TR, EN, ES, DE, AR, KO, RU
    static readonly Dictionary<string, string[]> Table = new()
    {
        ["play"]        = new[] { "OYNA", "PLAY", "JUGAR", "SPIELEN", "العب", "플레이", "ИГРАТЬ" },
        ["settings"]    = new[] { "Ayarlar", "Settings", "Ajustes", "Einstellungen", "الإعدادات", "설정", "Настройки" },
        ["worlds"]      = new[] { "Dünyalar", "Worlds", "Mundos", "Welten", "العوالم", "월드", "Миры" },
        ["back"]        = new[] { "Geri", "Back", "Atrás", "Zurück", "رجوع", "뒤로", "Назад" },
        ["continue"]    = new[] { "Devam Et", "Continue", "Continuar", "Weiter", "متابعة", "계속", "Продолжить" },
        ["resume"]      = new[] { "Devam Et", "Resume", "Reanudar", "Fortsetzen", "استئناف", "재개", "Продолжить" },
        ["hello"]       = new[] { "Merhaba,", "Hi,", "Hola,", "Hallo,", "مرحبا،", "안녕,", "Привет," },
        ["defaultPlayer"] = new[] { "Oyuncu", "Player", "Jugador", "Spieler", "لاعب", "플레이어", "Игрок" },
        ["loading"]     = new[] { "Yükleniyor", "Loading", "Cargando", "Laden", "جارٍ التحميل", "로딩 중", "Загрузка" },
        ["score"]       = new[] { "Skor:", "Score:", "Puntos:", "Punkte:", "النقاط:", "점수:", "Счёт:" },
        ["ok"]          = new[] { "Tamam", "OK", "OK", "OK", "حسنا", "확인", "ОК" },
        ["name"]        = new[] { "İsim", "Name", "Nombre", "Name", "الاسم", "이름", "Имя" },
        ["tellFriend"]  = new[] { "Arkadaşına Öner", "Tell a Friend", "Recomendar", "Empfehlen", "أخبر صديقا", "친구에게 알리기", "Рассказать другу" },
        ["rate"]        = new[] { "Değerlendir", "Rate Us", "Puntúanos", "Bewerten", "قيّمنا", "평가하기", "Оценить" },
        ["removeAds"]   = new[] { "Reklamları Kaldır", "Remove Ads", "Quitar anuncios", "Werbung entfernen", "إزالة الإعلانات", "광고 제거", "Убрать рекламу" },
        ["buy"]         = new[] { "Satın Al", "Buy", "Comprar", "Kaufen", "شراء", "구매", "Купить" },
        ["store"]       = new[] { "Mağaza", "Store", "Tienda", "Shop", "المتجر", "상점", "Магазин" },
        ["coins"]       = new[] { "Coin:", "Coins:", "Monedas:", "Münzen:", "العملات:", "코인:", "Монеты:" },
        ["linkReady"]   = new[] { "Bağlantı paylaşıma hazır", "Link ready to share", "Enlace listo para compartir", "Link zum Teilen bereit", "الرابط جاهز للمشاركة", "공유 링크 준비됨", "Ссылка готова" },
        ["noLives"]     = new[] { "Can yok! Yenilenmeyi bekle.", "No lives! Wait to refill.", "¡Sin vidas! Espera.", "Keine Leben! Warte.", "لا حياة! انتظر التجديد.", "생명 없음! 기다리세요.", "Нет жизней! Подождите." },

        ["sfx"]         = new[] { "Ses Efektleri", "Sound Effects", "Efectos", "Soundeffekte", "المؤثرات الصوتية", "효과음", "Звуки" },
        ["music"]       = new[] { "Müzik", "Music", "Música", "Musik", "الموسيقى", "음악", "Музыка" },
        ["vibration"]   = new[] { "Titreşim", "Vibration", "Vibración", "Vibration", "الاهتزاز", "진동", "Вибрация" },
        ["difficulty"]  = new[] { "Zorluk", "Difficulty", "Dificultad", "Schwierigkeit", "الصعوبة", "난이도", "Сложность" },
        ["on"]          = new[] { "Açık", "On", "Sí", "An", "تشغيل", "켬", "Вкл" },
        ["off"]         = new[] { "Kapalı", "Off", "No", "Aus", "إيقاف", "끔", "Выкл" },
        ["easy"]        = new[] { "Kolay", "Easy", "Fácil", "Leicht", "سهل", "쉬움", "Легко" },
        ["normal"]      = new[] { "Normal", "Normal", "Normal", "Normal", "عادي", "보통", "Средне" },
        ["hard"]        = new[] { "Zor", "Hard", "Difícil", "Schwer", "صعب", "어려움", "Сложно" },
        ["language"]    = new[] { "Dil", "Language", "Idioma", "Sprache", "اللغة", "언어", "Язык" },

        ["askName"]     = new[] { "Adın nedir?", "What's your name?", "¿Cómo te llamas?", "Wie heißt du?", "ما اسمك؟", "이름이 뭐예요?", "Как тебя зовут?" },
        ["changeName"]  = new[] { "İsmini değiştir", "Change your name", "Cambiar nombre", "Namen ändern", "غيّر اسمك", "이름 변경", "Изменить имя" },
        ["chooseLang"]  = new[] { "Dil Seç", "Choose Language", "Elegir idioma", "Sprache wählen", "اختر اللغة", "언어 선택", "Выбор языка" },

        ["redirectPay"] = new[] { "Ödemeye yönlendiriliyor...", "Redirecting to payment...", "Redirigiendo al pago...", "Weiterleitung zur Zahlung...", "جارٍ التحويل للدفع...", "결제로 이동 중...", "Переход к оплате..." },
        ["purchased"]   = new[] { "Satın alındı:", "Purchased:", "Comprado:", "Gekauft:", "تم الشراء:", "구매됨:", "Куплено:" },
        ["adsRemoved"]  = new[] { "Reklamlar kaldırıldı!", "Ads removed!", "¡Anuncios eliminados!", "Werbung entfernt!", "تمت إزالة الإعلانات!", "광고 제거됨!", "Реклама убрана!" },
        ["noAdsDesc"]   = new[] { "Tek seferlik ödeme ile tüm\nreklamlar kalıcı olarak kalkar.", "A one-time purchase removes\nall ads permanently.", "Una compra única elimina\ntodos los anuncios para siempre.", "Ein einmaliger Kauf entfernt\nalle Werbung dauerhaft.", "عملية شراء واحدة تزيل\nكل الإعلانات نهائيا.", "한 번의 구매로 모든\n광고가 영구 제거됩니다.", "Разовая покупка навсегда\nубирает всю рекламу." },
        ["pkStarter"]   = new[] { "Başlangıç Paketi", "Starter Pack", "Pack inicial", "Starterpaket", "حزمة البداية", "스타터 팩", "Стартовый набор" },
        ["pkCoinBag"]   = new[] { "Coin Kesesi", "Coin Bag", "Bolsa de monedas", "Münzbeutel", "كيس عملات", "코인 주머니", "Мешок монет" },
        ["pkMagnet"]    = new[] { "Mıknatıs Paketi", "Magnet Pack", "Pack imán", "Magnet-Paket", "حزمة المغناطيس", "자석 팩", "Набор магнитов" },
        ["pkSuper"]     = new[] { "Süper Paket", "Super Pack", "Súper pack", "Super-Paket", "الحزمة الخارقة", "슈퍼 팩", "Супер-набор" },
        ["pkSuper3"]    = new[] { "Süper Güç ×3", "Super Power ×3", "Súper poder ×3", "Superkraft ×3", "قوة خارقة ×3", "슈퍼 파워 ×3", "Супер-сила ×3" },

        ["puSpeed"]     = new[] { "Hız", "Speed", "Velocidad", "Tempo", "سرعة", "스피드", "Скорость" },
        ["puMagnet"]    = new[] { "Mıknatıs", "Magnet", "Imán", "Magnet", "مغناطيس", "자석", "Магнит" },
        ["puSize"]      = new[] { "Büyütme", "Grow", "Crecer", "Wachsen", "تكبير", "확대", "Рост" },
        ["puSuper"]     = new[] { "SÜPER", "SUPER", "SÚPER", "SUPER", "خارق", "슈퍼", "СУПЕР" },

        ["paused"]      = new[] { "Durduruldu", "Paused", "Pausa", "Pausiert", "متوقف", "일시정지", "Пауза" },
        ["quit"]        = new[] { "Çıkış", "Quit", "Salir", "Beenden", "خروج", "종료", "Выход" },
        ["retry"]       = new[] { "Tekrar Oyna", "Retry", "Reintentar", "Neu starten", "إعادة", "다시하기", "Заново" },

        ["congrats"]    = new[] { "TEBRİKLER!", "CONGRATS!", "¡FELICIDADES!", "GESCHAFFT!", "تهانينا!", "축하해요!", "ПОЗДРАВЛЯЕМ!" },
        ["nextLevel"]   = new[] { "Sonraki Bölüm", "Next Level", "Siguiente nivel", "Nächstes Level", "المستوى التالي", "다음 레벨", "Далее" },
        ["nextWorld"]   = new[] { "Sonraki Dünya", "Next World", "Siguiente mundo", "Nächste Welt", "العالم التالي", "다음 월드", "След. мир" },
        ["mainMenu"]    = new[] { "Ana Menü", "Main Menu", "Menú", "Hauptmenü", "القائمة", "메인 메뉴", "Меню" },
        ["reward"]      = new[] { "HEDİYE:", "REWARD:", "PREMIO:", "BELOHNUNG:", "مكافأة:", "보상:", "НАГРАДА:" },
        ["worldDone"]   = new[] { "Tamamlandı!", "Complete!", "¡Completado!", "Geschafft!", "اكتمل!", "완료!", "Готово!" },
        ["newWorld"]    = new[] { "Yeni dünya açıldı!", "New world unlocked!", "¡Nuevo mundo!", "Neue Welt!", "عالم جديد!", "새 월드 열림!", "Новый мир!" },
        ["timeUp"]      = new[] { "Süre doldu!", "Time's up!", "¡Se acabó el tiempo!", "Zeit abgelaufen!", "انتهى الوقت!", "시간 종료!", "Время вышло!" },
        ["bombExploded"] = new[] { "Bomba patladı!", "Bomb exploded!", "¡Explotó una bomba!", "Bombe explodiert!", "انفجرت قنبلة!", "폭탄 폭발!", "Бомба взорвалась!" },
        ["failTitle"]   = new[] { "OLMADI!", "FAILED!", "¡FALLASTE!", "VERLOREN!", "فشلت!", "실패!", "НЕУДАЧА!" },
        ["livesOut"]    = new[] { "Can bitti!", "Out of lives!", "¡Sin vidas!", "Keine Leben!", "نفدت الأرواح!", "생명 소진!", "Жизни кончились!" },
        ["nextLife"]    = new[] { "Sonraki can:", "Next life:", "Próxima vida:", "Nächstes Leben:", "الحياة التالية:", "다음 생명:", "След. жизнь:" },

        ["hudScore"]    = new[] { "Skor:", "Score:", "Puntos:", "Punkte:", "النقاط:", "점수:", "Счёт:" },
        ["hudTime"]     = new[] { "Süre:", "Time:", "Tiempo:", "Zeit:", "الوقت:", "시간:", "Время:" },
        ["level"]       = new[] { "BÖLÜM", "LEVEL", "NIVEL", "LEVEL", "المستوى", "레벨", "УРОВЕНЬ" },

        ["tipSpeed"]    = new[] { "HIZ!  Delik bir süre daha hızlı hareket eder", "SPEED!  The hole moves faster for a while", "¡VELOCIDAD!  El agujero se mueve más rápido", "TEMPO!  Das Loch bewegt sich schneller", "سرعة!  تتحرك الحفرة أسرع لفترة", "스피드!  구멍이 잠시 빨라집니다", "СКОРОСТЬ!  Дыра движется быстрее" },
        ["tipMagnet"]   = new[] { "MIKNATIS!  Yakındaki nesneler deliğe çekilir", "MAGNET!  Nearby objects are pulled in", "¡IMÁN!  Los objetos cercanos son atraídos", "MAGNET!  Objekte werden angezogen", "مغناطيس!  تنجذب الأشياء القريبة", "자석!  주변 물체가 끌려옵니다", "МАГНИТ!  Ближние объекты притягиваются" },
        ["tipSize"]     = new[] { "BÜYÜME!  Delik bir anda büyür", "GROW!  The hole grows instantly", "¡CRECER!  El agujero crece al instante", "WACHSEN!  Das Loch wächst sofort", "تكبير!  تكبر الحفرة فورا", "확대!  구멍이 즉시 커집니다", "РОСТ!  Дыра мгновенно растёт" },
        ["tipSuper"]    = new[] { "SÜPER GÜÇ!  Çevredeki her şey deliğe süpürülür, delik hızla büyür", "SUPER!  Everything nearby is swept in and the hole grows fast", "¡SÚPER!  Todo lo cercano es absorbido y el agujero crece rápido", "SUPER!  Alles wird eingesaugt und das Loch wächst schnell", "قوة خارقة!  يُسحب كل ما حولك وتكبر الحفرة بسرعة", "슈퍼!  주변 모든 것을 빨아들이고 구멍이 빠르게 커집니다", "СУПЕР!  Всё рядом всасывается, дыра быстро растёт" },
        ["flashGrow"]   = new[] { "BÜYÜME!", "GROW!", "¡CRECER!", "WACHSEN!", "تكبير!", "확대!", "РОСТ!" },
        ["flashSuper"]  = new[] { "SÜPER GÜÇ!", "SUPER!", "¡SÚPER!", "SUPER!", "قوة خارقة!", "슈퍼!", "СУПЕР!" },
        ["hudSpeed"]    = new[] { "HIZ", "SPEED", "VELOCIDAD", "TEMPO", "سرعة", "스피드", "СКОРОСТЬ" },
        ["hudMagnet"]   = new[] { "MIKNATIS", "MAGNET", "IMÁN", "MAGNET", "مغناطيس", "자석", "МАГНИТ" },

        ["hardLevel"]   = new[] { "ZOR BÖLÜM", "HARD LEVEL", "NIVEL DIFÍCIL", "SCHWERES LEVEL", "مستوى صعب", "어려운 레벨", "СЛОЖНЫЙ УРОВЕНЬ" },
        ["tutCollect"]  = new[] { "Bu nesneleri topla!", "Collect these!", "¡Recoge esto!", "Sammle diese!", "اجمع هذه!", "이것들을 모으세요!", "Собери это!" },
        ["tutDrag"]     = new[] { "Sürükleyerek yönlendir", "Drag to steer", "Arrastra para mover", "Ziehen zum Steuern", "اسحب للتحكم", "드래그로 조종", "Тяни для управления" },
        ["tutTap"]      = new[] { "Devam için dokun", "Tap to continue", "Toca para seguir", "Tippen zum Fortfahren", "انقر للمتابعة", "탭하여 계속", "Нажми, чтобы продолжить" },

        // Dünya adları
        ["w0"]  = new[] { "Park", "Park", "Parque", "Park", "حديقة", "공원", "Парк" },
        ["w1"]  = new[] { "Yiyecekler", "Foods", "Comida", "Essen", "طعام", "음식", "Еда" },
        ["w2"]  = new[] { "Arabalar", "Cars", "Coches", "Autos", "سيارات", "자동차", "Машины" },
        ["w3"]  = new[] { "Binalar", "Buildings", "Edificios", "Gebäude", "مبانٍ", "건물", "Здания" },
        ["w4"]  = new[] { "Tatlılar", "Sweets", "Dulces", "Süßes", "حلويات", "디저트", "Сладости" },
        ["w5"]  = new[] { "İçecekler", "Drinks", "Bebidas", "Getränke", "مشروبات", "음료", "Напитки" },
        ["w6"]  = new[] { "Hediyeler", "Gifts", "Regalos", "Geschenke", "هدايا", "선물", "Подарки" },
        ["w7"]  = new[] { "Kitaplar", "Books", "Libros", "Bücher", "كتب", "책", "Книги" },
        ["w8"]  = new[] { "Elektronik", "Electronics", "Electrónica", "Elektronik", "إلكترونيات", "전자제품", "Электроника" },
        ["w9"]  = new[] { "Kediler", "Cats", "Gatos", "Katzen", "قطط", "고양이", "Кошки" },
        ["w10"] = new[] { "Köpekler", "Dogs", "Perros", "Hunde", "كلاب", "개", "Собаки" },
        ["w11"] = new[] { "Gemiler", "Ships", "Barcos", "Schiffe", "سفن", "배", "Корабли" },
        ["w12"] = new[] { "Uçaklar", "Planes", "Aviones", "Flugzeuge", "طائرات", "비행기", "Самолёты" },
        ["w13"] = new[] { "Hazine", "Treasure", "Tesoro", "Schatz", "كنز", "보물", "Клад" },
        ["w14"] = new[] { "Altın", "Gold", "Oro", "Gold", "ذهب", "금", "Золото" },
        ["w15"] = new[] { "Mücevher", "Jewelry", "Joyas", "Schmuck", "مجوهرات", "보석", "Украшения" },
        ["w16"] = new[] { "Harikalar", "Wonders", "Maravillas", "Wunder", "عجائب", "불가사의", "Чудеса" },
        ["w17"] = new[] { "Karma", "Mixed", "Mixto", "Gemischt", "منوّع", "혼합", "Микс" },
    };

    public static string PowerName(PowerUpType t) => t switch
    {
        PowerUpType.Speed => T("puSpeed"), PowerUpType.Magnet => T("puMagnet"),
        PowerUpType.SizeBurst => T("puSize"), PowerUpType.Super => T("puSuper"), _ => "",
    };
}
