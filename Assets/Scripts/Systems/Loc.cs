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
        get => Chosen ? (Language)Mathf.Clamp(PlayerPrefs.GetInt(KEY, 0), 0, 6) : SystemDefault();
        set { PlayerPrefs.SetInt(KEY, (int)value); PlayerPrefs.SetInt(KEY_SET, 1); PlayerPrefs.Save(); }
    }

    /// <summary>
    /// Metin sağdan-sola script (Arapça/İbranice) içeriyor mu? Yön kararı DİLE değil METNE bakmalı:
    /// Arapça arayüzde bile Latin harfli bir oyuncu adı ("Onur") soldan-sağa yazılmalı
    /// (kullanıcı 2026-08-23: "Arapça'da Latin isim ters dönüyor").
    /// </summary>
    public static bool ContainsRtl(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s)
            if ((c >= 0x0590 && c <= 0x08FF) || (c >= 0xFB1D && c <= 0xFDFF) || (c >= 0xFE70 && c <= 0xFEFF)) return true;
        return false;
    }

    /// <summary>TMP metninin yazım yönünü İÇERİĞİNE göre ayarlar. Metni atadıktan SONRA çağır.</summary>
    public static void ApplyDir(TMPro.TMP_Text t)
    {
        if (t != null) t.isRightToLeftText = ContainsRtl(t.text);
    }

    /// <summary>Cihaz/sistem dilini 7 dilimizden birine eşler; desteklenmiyorsa İngilizce (en geniş taban).</summary>
    public static Language SystemDefault() => Application.systemLanguage switch
    {
        SystemLanguage.Turkish => Language.Turkish,
        SystemLanguage.English => Language.English,
        SystemLanguage.Spanish => Language.Spanish,
        SystemLanguage.German  => Language.German,
        SystemLanguage.Arabic  => Language.Arabic,
        SystemLanguage.Korean  => Language.Korean,
        SystemLanguage.Russian => Language.Russian,
        _ => Language.English,
    };

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
        ["noLivesTitle"] = new[] { "Canın Bitti!", "Out of Lives!", "¡Sin vidas!", "Keine Leben mehr!", "نفدت حياتك!", "생명이 없습니다!", "Жизни закончились!" },
        ["noLivesBody"] = new[] {
            "Oynamak için can gerekiyor. Hemen can al ya da yenilenmesini bekle.",
            "You need a life to play. Get one now or wait for it to refill.",
            "Necesitas una vida para jugar. Consigue una o espera a que se recargue.",
            "Zum Spielen brauchst du ein Leben. Hol dir eins oder warte auf die Auffüllung.",
            "تحتاج إلى حياة للعب. احصل على واحدة الآن أو انتظر التجديد.",
            "플레이하려면 생명이 필요합니다. 지금 얻거나 충전을 기다리세요.",
            "Для игры нужна жизнь. Получите её сейчас или дождитесь восстановления." },
        ["nextLifeIn"]  = new[] { "Sonraki can:", "Next life in:", "Próxima vida:", "Nächstes Leben:", "الحياة التالية:", "다음 생명:", "Следующая жизнь:" },
        ["lifeGained"]  = new[] { "+1 CAN", "+1 LIFE", "+1 VIDA", "+1 LEBEN", "+1 حياة", "+1 생명", "+1 ЖИЗНЬ" },
        ["extraReward"] = new[] { "EKSTRA KAZANÇ", "EXTRA REWARD", "RECOMPENSA EXTRA", "EXTRA-BELOHNUNG", "مكافأة إضافية", "추가 보상", "ДОПОЛНИТЕЛЬНАЯ НАГРАДА" },
        ["watchAdLife"] = new[] { "Reklam izle → +1 Can", "Watch ad → +1 Life", "Ver anuncio → +1 vida", "Werbung → +1 Leben", "شاهد إعلانًا ← +1 حياة", "광고 시청 → +1 생명", "Реклама → +1 жизнь" },
        ["watchAdTime"] = new[] { "İzle → +20sn Devam", "Watch → +20s Continue", "Ver → +20s continuar", "Werbung → +20s weiter", "شاهد ← +20ث متابعة", "광고 → +20초 계속", "Реклама → +20с" },
        ["watchAdContinue"] = new[] { "İzle → Devam Et", "Watch → Continue", "Ver → Continuar", "Werbung → Weiter", "شاهد ← متابعة", "광고 → 계속", "Реклама → Продолжить" },
        ["skipAdTitle"] = new[] { "Reklamı geç?", "Skip ad?", "¿Saltar anuncio?", "Werbung überspringen?", "تخطي الإعلان؟", "광고 건너뛰기?", "Пропустить рекламу?" },
        ["adLoading"]   = new[] { "Reklam yükleniyor…", "Loading ad…", "Cargando anuncio…", "Werbung lädt…", "جارٍ تحميل الإعلان…", "광고 로딩 중…", "Загрузка рекламы…" },
        ["adFailed"]    = new[] { "Reklam bulunamadı", "No ad available", "Sin anuncio", "Keine Werbung", "لا يوجد إعلان", "광고 없음", "Нет рекламы" },
        ["watchAd"]     = new[] { "Reklam İzle", "Watch Ad", "Ver anuncio", "Werbung", "شاهد إعلانًا", "광고 시청", "Смотреть рекламу" },
        ["watch"]       = new[] { "İzle", "Watch", "Ver", "Ansehen", "شاهد", "시청", "Смотреть" },
        ["secShort"]    = new[] { "sn", "s", "s", "s", "ث", "초", "с" },
        ["freeCoins"]   = new[] { "Bedava Coin", "Free Coins", "Monedas gratis", "Gratis-Münzen", "عملات مجانية", "무료 코인", "Бесплатные монеты" },
        ["free"]        = new[] { "Bedava", "Free", "Gratis", "Gratis", "مجاني", "무료", "Бесплатно" },
        ["coinWord"]    = new[] { "Coin", "Coins", "Monedas", "Münzen", "عملات", "코인", "Монеты" },
        ["powerupsTitle"] = new[] { "Güçler", "Power-Ups", "Potenciadores", "Power-Ups", "القوى", "파워업", "Усиления" },
        ["packagesTitle"] = new[] { "Paketler", "Packages", "Paquetes", "Pakete", "الحزم", "패키지", "Наборы" },
        ["notEnoughCoins"] = new[] { "Yetersiz coin!", "Not enough coins!", "¡Monedas insuficientes!", "Zu wenig Münzen!", "عملات غير كافية!", "코인 부족!", "Недостаточно монет!" },
        ["bought"]      = new[] { "Alındı!", "Purchased!", "¡Comprado!", "Gekauft!", "تم الشراء!", "구매됨!", "Куплено!" },
        ["storeUnavailable"] = new[] { "Mağaza şu an kullanılamıyor", "Store is unavailable right now", "La tienda no está disponible", "Shop derzeit nicht verfügbar", "المتجر غير متاح حاليًا", "상점을 사용할 수 없습니다", "Магазин сейчас недоступен" },
        ["refillLife"]  = new[] { "Can Yenile", "Refill Life", "Rellenar vida", "Leben auffüllen", "تجديد الحياة", "생명 충전", "Восполнить жизнь" },
        ["continueGame"] = new[] { "Devam Et", "Continue", "Continuar", "Weiter", "متابعة", "계속하기", "Продолжить" },
        ["doubleReward"] = new[] { "2X Ödül", "2X Reward", "2X Recompensa", "2X Belohnung", "2X مكافأة", "2X 보상", "2X Награда" },

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
        ["account"]     = new[] { "Hesap", "Account", "Cuenta", "Konto", "الحساب", "계정", "Аккаунт" },
        ["save_progress"] = new[] { "İlerlemeyi Kaydet", "Save Progress", "Guardar progreso", "Fortschritt sichern", "حفظ التقدم", "진행 상황 저장", "Сохранить прогресс" },
        ["account_linked"] = new[] { "✓ Bağlı", "✓ Linked", "✓ Vinculado", "✓ Verbunden", "✓ مرتبط", "✓ 연결됨", "✓ Связан" },
        ["deleteAccount"] = new[] { "Hesabı ve Verileri Sil", "Delete Account & Data", "Eliminar cuenta y datos", "Konto & Daten löschen", "حذف الحساب والبيانات", "계정 및 데이터 삭제", "Удалить аккаунт и данные" },
        ["deleteSure"]  = new[] { "Emin misin?", "Are you sure?", "¿Estás seguro?", "Bist du sicher?", "هل أنت متأكد؟", "정말 삭제할까요?", "Вы уверены?" },
        ["deleteWarn"]  = new[] {
            "Hesabın, bulut kaydın ve tüm ilerlemen (coin, yıldız, güç-up'lar) kalıcı olarak silinir. Bu işlem GERİ ALINAMAZ.",
            "Your account, cloud save and all progress (coins, stars, power-ups) will be permanently deleted. This CANNOT be undone.",
            "Tu cuenta, tu guardado en la nube y todo tu progreso (monedas, estrellas, potenciadores) se eliminarán de forma permanente. NO se puede deshacer.",
            "Dein Konto, dein Cloud-Speicher und dein gesamter Fortschritt (Münzen, Sterne, Power-Ups) werden dauerhaft gelöscht. Dies kann NICHT rückgängig gemacht werden.",
            "سيتم حذف حسابك ونسختك السحابية وكل تقدمك (العملات، النجوم، القوى) نهائيًا. لا يمكن التراجع عن هذا الإجراء.",
            "계정, 클라우드 저장 데이터, 모든 진행 상황(코인, 별, 파워업)이 영구 삭제됩니다. 되돌릴 수 없습니다.",
            "Ваш аккаунт, облачное сохранение и весь прогресс (монеты, звёзды, усиления) будут удалены навсегда. Это НЕЛЬЗЯ отменить." },
        ["deleteYes"]   = new[] { "Evet, Sil", "Yes, Delete", "Sí, eliminar", "Ja, löschen", "نعم، احذف", "네, 삭제", "Да, удалить" },
        ["cancel"]      = new[] { "Vazgeç", "Cancel", "Cancelar", "Abbrechen", "إلغاء", "취소", "Отмена" },
        ["deleteDone"]  = new[] { "Hesap ve veriler silindi", "Account and data deleted", "Cuenta y datos eliminados", "Konto und Daten gelöscht", "تم حذف الحساب والبيانات", "계정과 데이터가 삭제되었습니다", "Аккаунт и данные удалены" },
        ["deleteFail"]  = new[] { "Silme başarısız — bağlantını kontrol et", "Deletion failed — check your connection", "Error al eliminar — revisa tu conexión", "Löschen fehlgeschlagen — Verbindung prüfen", "فشل الحذف — تحقق من اتصالك", "삭제 실패 — 연결을 확인하세요", "Не удалось удалить — проверьте соединение" },

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
        ["pkSuper3"]    = new[] { "Süper Güç ×6", "Super Power ×6", "Súper poder ×6", "Superkraft ×6", "قوة خارقة ×6", "슈퍼 파워 ×6", "Супер-сила ×6" },

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
        ["gained"]      = new[] { "Kazanılan:", "Earned:", "Ganado:", "Erhalten:", "المكتسب:", "획득:", "Получено:" },
        ["pwSpeed"]     = new[] { "Hız", "Speed", "Velocidad", "Tempo", "سرعة", "속도", "Скорость" },
        ["pwMagnet"]    = new[] { "Mıknatıs", "Magnet", "Imán", "Magnet", "مغناطيس", "자석", "Магнит" },
        ["pwSize"]      = new[] { "Büyütme", "Grow", "Agrandar", "Vergrößern", "تكبير", "확대", "Увеличение" },
        ["pwSuper"]     = new[] { "SÜPER", "SUPER", "SÚPER", "SUPER", "سوبر", "슈퍼", "СУПЕР" },
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

    // Mağaza yer-tutucu fiyatları BÖLGEYE göre (gerçek IAP fiyatları store'dan gelene kadar). Font'ta olmayan
    // ₺ sembolü yerine bölgeye uygun sembol/kod: TR→TL, EN/AR/KO→$, ES/DE→€, RU→₽ (font Latin+Kiril+€ destekler).
    // tier: 0=başlangıç 1=coin torbası 2=mıknatıs 3=süper 4=süper3.
    public static string Price(int tier)
    {
        tier = Mathf.Clamp(tier, 0, 4);
        switch (Current)
        {
            case Language.Turkish: return new[] { "9,99 TL", "14,99 TL", "19,99 TL", "29,99 TL", "24,99 TL" }[tier];
            case Language.Russian: return new[] { "99 ₽", "149 ₽", "199 ₽", "299 ₽", "249 ₽" }[tier];
            case Language.German:
            case Language.Spanish: return new[] { "0,99 €", "1,49 €", "1,99 €", "2,99 €", "2,49 €" }[tier];
            default:               return new[] { "$0.99", "$1.49", "$1.99", "$2.99", "$2.49" }[tier];   // en, ar, ko → $
        }
    }
}
